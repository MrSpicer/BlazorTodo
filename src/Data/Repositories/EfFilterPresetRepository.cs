using Microsoft.EntityFrameworkCore;
using TodoList.Identity;
using TodoList.Models;

namespace TodoList.Data.Repositories;

public class EfFilterPresetRepository : EfRepositoryBase, IFilterPresetRepository
{
	private readonly FilterPresetRepository _localFallbackForSettings;

	public EfFilterPresetRepository(
		IDbContextFactory<AppDbContext> dbFactory,
		ICurrentUserContext user,
		FilterPresetRepository localFallbackForSettings) : base(dbFactory, user)
	{
		_localFallbackForSettings = localFallbackForSettings;
	}

	public Task InitializeAsync() => _localFallbackForSettings.InitializeAsync();

	public async Task<List<FilterPreset>> GetPresetsAsync()
	{
		var userId = RequireUserId();
		await using var db = await CreateDbAsync();
		return await db.FilterPresets
			.AsNoTracking()
			.Where(p => p.UserId == userId)
			.ToListAsync();
	}

	public async Task<bool> SaveAsync(FilterPreset preset)
	{
		if (preset is null || string.IsNullOrWhiteSpace(preset.Name)) return false;
		var userId = RequireUserId();

		await using var db = await CreateDbAsync();
		var existing = await db.FilterPresets
			.FirstOrDefaultAsync(p => p.UserId == userId && p.Name.ToLower() == preset.Name.ToLower());

		var toPersist = preset with { UserId = userId, Id = existing?.Id ?? preset.Id };
		if (existing is null)
		{
			db.FilterPresets.Add(toPersist);
		}
		else
		{
			db.Entry(existing).CurrentValues.SetValues(toPersist);
		}
		await db.SaveChangesAsync();
		return true;
	}

	public async Task<bool> DeleteAsync(string name)
	{
		if (string.IsNullOrWhiteSpace(name)) return false;
		var userId = RequireUserId();
		await using var db = await CreateDbAsync();
		var deleted = await db.FilterPresets
			.Where(p => p.UserId == userId && p.Name.ToLower() == name.ToLower())
			.ExecuteDeleteAsync();
		return deleted > 0;
	}

	// FilterPresetSettings are kept in LocalStorage even for authenticated users for v1.
	public Task<FilterPresetSettings> GetSettingsAsync() => _localFallbackForSettings.GetSettingsAsync();

	public Task<bool> SaveSettingsAsync(FilterPresetSettings settings) => _localFallbackForSettings.SaveSettingsAsync(settings);
}
