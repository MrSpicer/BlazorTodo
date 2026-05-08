using Blazored.LocalStorage;
using TodoList.Models;

namespace TodoList.Data;

public class FilterPresetRepository : IFilterPresetRepository
{
	private const string PresetsKey = "FilterPresets";
	private const string SettingsKey = "FilterPresetSettings";

	private readonly ILogger<FilterPresetRepository> _logger;
	private readonly ILocalStorageService _localStorage;

	private List<FilterPreset> _presets = new();

	public FilterPresetRepository(ILogger<FilterPresetRepository> logger, ILocalStorageService localStorage)
	{
		_logger = logger;
		_localStorage = localStorage;
	}

	public async Task InitializeAsync()
	{
		try
		{
			_presets = await _localStorage.GetItemAsync<List<FilterPreset>>(PresetsKey) ?? new List<FilterPreset>();
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Error loading filter presets");
			_presets = new List<FilterPreset>();
		}
	}

	public Task<List<FilterPreset>> GetPresetsAsync()
	{
		return Task.FromResult(_presets.ToList());
	}

	public async Task<bool> SaveAsync(FilterPreset preset)
	{
		if (preset == null || string.IsNullOrWhiteSpace(preset.Name))
		{
			_logger.LogWarning("Malformed filter preset");
			return false;
		}

		try
		{
			var existingIndex = _presets.FindIndex(p => p.Name.Equals(preset.Name, StringComparison.OrdinalIgnoreCase));
			if (existingIndex >= 0)
				_presets[existingIndex] = preset;
			else
				_presets.Add(preset);

			await _localStorage.SetItemAsync(PresetsKey, _presets);
			return true;
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Error saving filter preset {Name}", preset.Name);
			return false;
		}
	}

	public async Task<bool> DeleteAsync(string name)
	{
		try
		{
			var removed = _presets.RemoveAll(p => p.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
			if (removed == 0) return false;
			await _localStorage.SetItemAsync(PresetsKey, _presets);
			return true;
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Error deleting filter preset {Name}", name);
			return false;
		}
	}

	public async Task<FilterPresetSettings> GetSettingsAsync()
	{
		try
		{
			return await _localStorage.GetItemAsync<FilterPresetSettings>(SettingsKey) ?? FilterPresetSettings.Default;
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Error loading filter preset settings");
			return FilterPresetSettings.Default;
		}
	}

	public async Task<bool> SaveSettingsAsync(FilterPresetSettings settings)
	{
		try
		{
			await _localStorage.SetItemAsync(SettingsKey, settings);
			return true;
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Error saving filter preset settings");
			return false;
		}
	}
}
