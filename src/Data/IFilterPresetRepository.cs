using TodoList.Models;

namespace TodoList.Data;

public interface IFilterPresetRepository
{
	Task InitializeAsync();
	Task<List<FilterPreset>> GetPresetsAsync();
	Task<bool> SaveAsync(FilterPreset preset);
	Task<bool> DeleteAsync(string name);
	Task<FilterPresetSettings> GetSettingsAsync();
	Task<bool> SaveSettingsAsync(FilterPresetSettings settings);
}
