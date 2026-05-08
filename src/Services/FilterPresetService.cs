using TodoList.Data;
using TodoList.Models;

namespace TodoList.Services;

public class FilterPresetService : IFilterPresetService
{
	private readonly IFilterPresetRepository _repository;

	private List<FilterPreset> _userPresets = new();
	private FilterPresetSettings _settings = FilterPresetSettings.Default;

	public event Action? OnPresetsChanged;

	public FilterPresetService(IFilterPresetRepository repository)
	{
		_repository = repository;
	}

	public IReadOnlyList<FilterPreset> Presets =>
		new[] { FilterPreset.SystemDefault }
			.Concat(_userPresets.OrderBy(p => p.Name, StringComparer.OrdinalIgnoreCase))
			.ToList();

	public bool RememberPresets => _settings.RememberPresets;

	public string? RememberedPresetName => _settings.RememberedPresetName;

	public async Task InitializeAsync()
	{
		await _repository.InitializeAsync();
		_userPresets = await _repository.GetPresetsAsync();
		_settings = await _repository.GetSettingsAsync();
		NotifyStateChanged();
	}

	public FilterPreset? GetByName(string name)
	{
		if (IsProtected(name)) return FilterPreset.SystemDefault;
		return _userPresets.FirstOrDefault(p => p.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
	}

	public bool Exists(string name)
	{
		if (IsProtected(name)) return true;
		return _userPresets.Any(p => p.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
	}

	public bool IsProtected(string name) =>
		!string.IsNullOrWhiteSpace(name) &&
		name.Trim().Equals(FilterPreset.DefaultName, StringComparison.OrdinalIgnoreCase);

	public async Task<bool> SaveAsync(FilterPreset preset)
	{
		if (preset == null) return false;
		if (IsProtected(preset.Name)) return false;

		var success = await _repository.SaveAsync(preset);
		if (success)
		{
			_userPresets = await _repository.GetPresetsAsync();
			NotifyStateChanged();
		}
		return success;
	}

	public async Task<bool> DeleteAsync(string name)
	{
		if (IsProtected(name)) return false;

		var success = await _repository.DeleteAsync(name);
		if (success)
		{
			_userPresets = await _repository.GetPresetsAsync();

			// If the deleted preset was the remembered selection, clear it.
			if (_settings.RememberedPresetName != null &&
				_settings.RememberedPresetName.Equals(name, StringComparison.OrdinalIgnoreCase))
			{
				_settings = _settings with { RememberedPresetName = null };
				await _repository.SaveSettingsAsync(_settings);
			}

			NotifyStateChanged();
		}
		return success;
	}

	public async Task SetRememberPresetsAsync(bool enabled, string? currentSelection = null)
	{
		var seed = enabled ? currentSelection : null;
		if (_settings.RememberPresets == enabled &&
			_settings.RememberedPresetName == seed) return;

		var next = enabled
			? new FilterPresetSettings(true, seed)
			: new FilterPresetSettings(false, null);

		if (await _repository.SaveSettingsAsync(next))
		{
			_settings = next;
			NotifyStateChanged();
		}
	}

	public async Task SetRememberedPresetNameAsync(string? name)
	{
		if (!_settings.RememberPresets) return;
		if (_settings.RememberedPresetName == name) return;

		var next = _settings with { RememberedPresetName = name };
		if (await _repository.SaveSettingsAsync(next))
		{
			_settings = next;
		}
	}

	private void NotifyStateChanged() => OnPresetsChanged?.Invoke();
}
