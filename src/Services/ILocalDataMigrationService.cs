namespace TodoList.Services;

/// <summary>
/// Migrates data created anonymously (browser localStorage) into the signed-in user's
/// account (Postgres). Anonymous data becomes invisible once authenticated because the
/// routing repositories switch to the empty EF backend; this service offers a one-time
/// upload so that data isn't lost.
/// </summary>
public interface ILocalDataMigrationService
{
	/// <summary>True if this browser holds any local todos, projects, or notes.</summary>
	Task<bool> HasLocalDataAsync();

	/// <summary>True when we should show the migration prompt: authenticated, not opted out, and local data exists.</summary>
	Task<bool> ShouldPromptAsync();

	/// <summary>Persists the per-device "don't ask again" flag.</summary>
	Task SetDontAskAgainAsync();

	/// <summary>
	/// Uploads local data into the authenticated account (merge semantics). On success the
	/// local todos/projects/notes/tags/statuses/priorities are cleared; on failure they are
	/// left untouched. Filter presets and the don't-ask flag are never cleared.
	/// </summary>
	Task<ImportResult> MigrateAsync();

	/// <summary>Clears the six local data stores (todos, projects, notes, tags, statuses, priorities). Leaves filter presets and settings intact.</summary>
	Task ClearLocalDataAsync();
}
