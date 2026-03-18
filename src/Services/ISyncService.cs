namespace TodoList.Services;

public record SyncResult(bool Success, string? ErrorMessage = null, bool RateLimited = false, int SecondsUntilRetry = 0);
public record RestoreResult(bool Success, string? ErrorMessage = null, bool NotFound = false, int ImportedCount = 0);

public interface ISyncService
{
	Task<SyncResult> SyncToServerAsync(string sessionId);
	Task<RestoreResult> RestoreFromServerAsync(string sessionId);
	string GenerateQrCodeSvg(string url);
}
