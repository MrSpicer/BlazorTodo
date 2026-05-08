namespace TodoList.Models;

public record FilterPresetSettings(bool RememberPresets, string? RememberedPresetName)
{
	public static FilterPresetSettings Default { get; } = new(false, null);
}
