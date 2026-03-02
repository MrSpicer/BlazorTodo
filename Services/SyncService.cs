using Microsoft.Extensions.Caching.Memory;
using Net.Codecrete.QrCodeGenerator;

namespace TodoList.Services;

public class SyncService : ISyncService
{
	private readonly IImportExportService _importExportService;
	private readonly IMemoryCache _cache;
	private readonly ILogger<SyncService> _logger;

	private static readonly TimeSpan DataExpiry = TimeSpan.FromHours(48);
	private static readonly TimeSpan RateLimitWindow = TimeSpan.FromMinutes(15);

	public SyncService(IImportExportService importExportService, IMemoryCache cache, ILogger<SyncService> logger)
	{
		_importExportService = importExportService;
		_cache = cache;
		_logger = logger;
	}

	public async Task<SyncResult> SyncToServerAsync(string sessionId)
	{
		try
		{
			var rateLimitKey = $"ratelimit:{sessionId}";
			if (_cache.TryGetValue(rateLimitKey, out DateTimeOffset lastSync))
			{
				var elapsed = DateTimeOffset.UtcNow - lastSync;
				var remaining = RateLimitWindow - elapsed;
				if (remaining > TimeSpan.Zero)
				{
					return new SyncResult(false, "Rate limit exceeded. Please wait before syncing again.",
						RateLimited: true, SecondsUntilRetry: (int)remaining.TotalSeconds);
				}
			}

			var json = await _importExportService.ExportToJsonAsync();

			const int MaxBytes = 5 * 1024 * 1024;
			if (System.Text.Encoding.UTF8.GetByteCount(json) > MaxBytes)
			{
				_logger.LogWarning("Sync data for session {SessionId} exceeds 5MB limit", sessionId);
				return new SyncResult(false, "Data exceeds the 5MB limit and cannot be synced.");
			}

			var dataKey = $"syncdata:{sessionId}";

			_cache.Set(dataKey, json, DataExpiry);
			_cache.Set(rateLimitKey, DateTimeOffset.UtcNow, RateLimitWindow);

			_logger.LogInformation("Synced data for session {SessionId}", sessionId);
			return new SyncResult(true);
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Failed to sync data for session {SessionId}", sessionId);
			return new SyncResult(false, "An error occurred while syncing. Please try again.");
		}
	}

	public async Task<RestoreResult> RestoreFromServerAsync(string sessionId)
	{
		try
		{
			var dataKey = $"syncdata:{sessionId}";
			if (!_cache.TryGetValue(dataKey, out string? json) || json is null)
			{
				return new RestoreResult(false, "No data found for this session ID. It may have expired.", NotFound: true);
			}

			var result = await _importExportService.ImportFromJsonAsync(json, replaceExisting: true);
			if (!result.Success)
			{
				return new RestoreResult(false, result.ErrorMessage ?? "Import failed.");
			}

			_logger.LogInformation("Restored {Count} items for session {SessionId}", result.ImportedCount, sessionId);
			return new RestoreResult(true, ImportedCount: result.ImportedCount);
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Failed to restore data for session {SessionId}", sessionId);
			return new RestoreResult(false, "An error occurred while restoring. Please try again.");
		}
	}

	public string GenerateQrCodeSvg(string url)
	{
		var qr = QrCode.EncodeText(url, QrCode.Ecc.Medium);
		var svg = qr.ToSvgString(4);
		// Library only generates a viewBox with no pixel dimensions; inject explicit size
		return svg.Replace("<svg ", "<svg width=\"200\" height=\"200\" ");
	}
}
