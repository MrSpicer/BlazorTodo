using TodoList.Identity;
using TodoList.Models;

namespace TodoList.Data.Repositories;

public class RoutingFilterPresetRepository : IFilterPresetRepository
{
	private readonly FilterPresetRepository _local;
	private readonly EfFilterPresetRepository _server;
	private readonly ICurrentUserContext _user;

	public RoutingFilterPresetRepository(
		FilterPresetRepository local,
		EfFilterPresetRepository server,
		ICurrentUserContext user)
	{
		_local = local;
		_server = server;
		_user = user;
	}

	private IFilterPresetRepository Active => _user.IsAuthenticated ? _server : _local;

	public Task InitializeAsync() => Active.InitializeAsync();
	public Task<List<FilterPreset>> GetPresetsAsync() => Active.GetPresetsAsync();
	public Task<bool> SaveAsync(FilterPreset preset) => Active.SaveAsync(preset);
	public Task<bool> DeleteAsync(string name) => Active.DeleteAsync(name);

	// Settings (rememberPresets / rememberedPresetName) always live in LocalStorage for v1.
	public Task<FilterPresetSettings> GetSettingsAsync() => _local.GetSettingsAsync();
	public Task<bool> SaveSettingsAsync(FilterPresetSettings settings) => _local.SaveSettingsAsync(settings);
}
