namespace TodoList.Models.Enums;

/// <summary>
/// Per-role, per-project capabilities. Stored as an integer bitmask on
/// <see cref="TodoList.Models.ProjectAccessRole"/> (one row per (project, role)). The project
/// owner implicitly has <see cref="All"/> and never gets a stored grant. Bits are grouped by
/// resource (Todos, Notes, Reference data, Project settings, Members) × action (Read/Add/Modify/
/// Remove). Some (resource, action) pairs are not meaningful and simply go unused:
/// Project has no Add (projects are created globally) and its Read is implied by membership;
/// Reference-data Read is likewise always granted so shared todos can resolve their labels.
/// </summary>
[Flags]
public enum ProjectPermission
{
	None = 0,

	TodosRead = 1 << 0,
	TodosAdd = 1 << 1,
	TodosModify = 1 << 2,
	TodosRemove = 1 << 3,

	NotesRead = 1 << 4,
	NotesAdd = 1 << 5,
	NotesModify = 1 << 6,
	NotesRemove = 1 << 7,

	ReferenceRead = 1 << 8,
	ReferenceAdd = 1 << 9,
	ReferenceModify = 1 << 10,
	ReferenceRemove = 1 << 11,

	ProjectModify = 1 << 12,
	ProjectRemove = 1 << 13,

	MembersRead = 1 << 14,
	MembersInvite = 1 << 15,
	MembersModify = 1 << 16,
	MembersRemove = 1 << 17,

	AllTodos = TodosRead | TodosAdd | TodosModify | TodosRemove,
	AllNotes = NotesRead | NotesAdd | NotesModify | NotesRemove,
	AllReference = ReferenceRead | ReferenceAdd | ReferenceModify | ReferenceRemove,
	ManageMembers = MembersRead | MembersInvite | MembersModify | MembersRemove,

	/// <summary>The access an accepted member had before granular permissions existed:
	/// full read/write over todos and notes plus read of the owner's reference data.</summary>
	DefaultMember = AllTodos | AllNotes | ReferenceRead,

	/// <summary>Every capability. The effective permission set of a project owner.</summary>
	All = AllTodos | AllNotes | AllReference | ProjectModify | ProjectRemove | ManageMembers
}
