using Blazored.LocalStorage;
using TodoList.Data;
using TodoList.Identity;
using TodoList.Realtime;

namespace TodoList.Services;

public class LocalDataMigrationService : ILocalDataMigrationService, IDisposable
{
	// Per-device opt-out. No separate "migrated" marker is needed: a successful migration
	// clears the local data, so HasLocalDataAsync() returning false is the marker.
	private const string DontAskKey = "LocalDataMigration_DontAsk";

	// Concrete localStorage repos — auth-agnostic, so they read/write the browser store even
	// while authenticated (the routing repos would otherwise dispatch to Postgres).
	private readonly TodoRepository _todoRepo;
	private readonly ProjectRepository _projectRepo;
	private readonly NoteRepository _noteRepo;
	private readonly TagRepository _tagRepo;
	private readonly StatusRepository _statusRepo;
	private readonly PriorityRepository _priorityRepo;

	private readonly ILocalStorageService _localStorage;
	private readonly ICurrentUserContext _user;
	private readonly IImportExportService _importExport;
	private readonly IUserChangeBus _bus;
	private readonly ILogger<LocalDataMigrationService> _logger;

	// The concrete localStorage repos are scoped — one instance per circuit, shared by every
	// caller here. The Settings page (HasLocalDataAsync) and the MainLayout prompt
	// (ShouldPromptAsync) can fire around the same first render, so serialize all repo access
	// to avoid interleaving InitializeAsync() (which clears+repopulates the repo's index) with
	// a concurrent GetAll() enumeration ("Collection was modified").
	private readonly SemaphoreSlim _gate = new(1, 1);

	public LocalDataMigrationService(
		TodoRepository todoRepo,
		ProjectRepository projectRepo,
		NoteRepository noteRepo,
		TagRepository tagRepo,
		StatusRepository statusRepo,
		PriorityRepository priorityRepo,
		ILocalStorageService localStorage,
		ICurrentUserContext user,
		IImportExportService importExport,
		IUserChangeBus bus,
		ILogger<LocalDataMigrationService> logger)
	{
		_todoRepo = todoRepo;
		_projectRepo = projectRepo;
		_noteRepo = noteRepo;
		_tagRepo = tagRepo;
		_statusRepo = statusRepo;
		_priorityRepo = priorityRepo;
		_localStorage = localStorage;
		_user = user;
		_importExport = importExport;
		_bus = bus;
		_logger = logger;
	}

	public async Task<bool> HasLocalDataAsync()
	{
		await _gate.WaitAsync();
		try { return await HasLocalDataCoreAsync(); }
		finally { _gate.Release(); }
	}

	private async Task<bool> HasLocalDataCoreAsync()
	{
		try
		{
			await _projectRepo.InitializeAsync();
			if ((await _projectRepo.GetAll()).Count > 0)
				return true;

			await _todoRepo.InitializeAsync();
			if ((await _todoRepo.GetAll()).Count > 0)
				return true;

			await _noteRepo.InitializeAsync();
			return (await _noteRepo.GetAll()).Count > 0;
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Failed to probe local data");
			return false;
		}
	}

	public async Task<bool> ShouldPromptAsync()
	{
		if (!_user.IsAuthenticated)
			return false;

		await _gate.WaitAsync();
		try
		{
			bool dontAsk;
			try
			{
				dontAsk = await _localStorage.GetItemAsync<bool>(DontAskKey);
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Failed to read migration opt-out flag");
				dontAsk = false;
			}
			if (dontAsk)
				return false;

			return await HasLocalDataCoreAsync();
		}
		finally { _gate.Release(); }
	}

	public Task SetDontAskAgainAsync() =>
		_localStorage.SetItemAsync(DontAskKey, true).AsTask();

	public async Task<ImportResult> MigrateAsync()
	{
		if (!_user.IsAuthenticated)
			return new ImportResult { Success = false, ErrorMessage = "You must be signed in to upload local data." };

		await _gate.WaitAsync();
		try { return await MigrateCoreAsync(); }
		finally { _gate.Release(); }
	}

	private async Task<ImportResult> MigrateCoreAsync()
	{
		try
		{
			await _projectRepo.InitializeAsync();
			await _todoRepo.InitializeAsync();
			await _noteRepo.InitializeAsync();
			await _tagRepo.InitializeAsync();
			await _statusRepo.InitializeAsync();
			await _priorityRepo.InitializeAsync();

			var document = new AppDataDocument
			{
				ExportedAt = DateTime.UtcNow,
				Projects = await _projectRepo.GetAll(),
				Todos = await _todoRepo.GetAll(),
				Notes = await _noteRepo.GetAll(),
				Tags = await _tagRepo.GetAll(),
				Statuses = await _statusRepo.GetAll(),
				Priorities = await _priorityRepo.GetAll()
			};

			// Merge into the account (routing services write to Postgres while authenticated).
			var result = await _importExport.ImportAsync(document, replaceExisting: false);

			if (result.Success)
			{
				await ClearLocalDataCoreAsync();

				// Refresh sibling circuits (other devices/tabs) for this user.
				var userId = _user.UserId;
				await _bus.PublishAsync(new UserChangeEvent(userId, ChangeKind.Projects));
				await _bus.PublishAsync(new UserChangeEvent(userId, ChangeKind.Todos));
				await _bus.PublishAsync(new UserChangeEvent(userId, ChangeKind.Notes));
			}

			return result;
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Local data migration failed");
			return new ImportResult { Success = false, ErrorMessage = "Upload failed due to an unexpected error." };
		}
	}

	public async Task ClearLocalDataAsync()
	{
		await _gate.WaitAsync();
		try { await ClearLocalDataCoreAsync(); }
		finally { _gate.Release(); }
	}

	private async Task ClearLocalDataCoreAsync()
	{
		// Deliberately excludes FilterPresets / FilterPresetSettings (per-device UI preferences)
		// and the don't-ask flag. Initialize first so each repo knows which entity keys to remove.
		await _projectRepo.InitializeAsync();
		await _projectRepo.ClearAll();

		await _todoRepo.InitializeAsync();
		await _todoRepo.ClearAll();

		await _noteRepo.InitializeAsync();
		await _noteRepo.ClearAll();

		await _tagRepo.InitializeAsync();
		await _tagRepo.ClearAll();

		await _statusRepo.InitializeAsync();
		await _statusRepo.ClearAll();

		await _priorityRepo.InitializeAsync();
		await _priorityRepo.ClearAll();
	}

	public void Dispose() => _gate.Dispose();
}
