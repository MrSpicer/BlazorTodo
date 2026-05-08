using TodoList.Models;

namespace TodoList.Services;

public interface IFilterPresetService
{
	event Action? OnPresetsChanged;

	IReadOnlyList<FilterPreset> Presets { get; }
	bool RememberPresets { get; }
	string? RememberedPresetName { get; }

	Task InitializeAsync();
	FilterPreset? GetByName(string name);
	bool Exists(string name);
	bool IsProtected(string name);
	Task<bool> SaveAsync(FilterPreset preset);
	Task<bool> DeleteAsync(string name);
	Task SetRememberPresetsAsync(bool enabled, string? currentSelection = null);
	Task SetRememberedPresetNameAsync(string? name);
}
