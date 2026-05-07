namespace TodoList.Models;

public static class BuiltInStatusIds
{
	public static readonly Guid None       = new("00000001-0000-0000-0000-000000000001");
	public static readonly Guid New        = new("00000002-0000-0000-0000-000000000001");
	public static readonly Guid InProgress = new("00000003-0000-0000-0000-000000000001");
	public static readonly Guid Done       = new("00000004-0000-0000-0000-000000000001");
	public static readonly Guid Abandoned  = new("00000005-0000-0000-0000-000000000001");
	public static readonly Guid Archived   = new("00000006-0000-0000-0000-000000000001");

	public static readonly IReadOnlyList<Guid> AllIds = new[]
	{
		None, New, InProgress, Done, Abandoned, Archived
	};

	public static readonly HashSet<Guid> CompletedLikeIds = new() { Done, Abandoned, Archived };

	public static readonly IReadOnlyList<Guid> DefaultFilterIds = new[] { None, New, InProgress };

	public static bool IsCompletedLike(Guid id) => CompletedLikeIds.Contains(id);

	public static bool IsBuiltIn(Guid id) => AllIds.Contains(id);

	public static Guid FromLegacyEnum(int legacyValue) => legacyValue switch
	{
		0 => None,
		1 => New,
		2 => InProgress,
		3 => Done,
		4 => Abandoned,
		5 => Archived,
		_ => None
	};

	public static int NaturalOrder(Guid id)
	{
		if (id == None) return 0;
		if (id == New) return 1;
		if (id == InProgress) return 2;
		if (id == Done) return 3;
		if (id == Abandoned) return 4;
		if (id == Archived) return 5;
		return int.MaxValue;
	}

	public static IReadOnlyList<Status> Seed() => new List<Status>
	{
		new() { Id = None,       Name = "None",        Color = "#f3f4f6", IsBuiltIn = true },
		new() { Id = New,        Name = "New",         Color = "#dbeafe", IsBuiltIn = true },
		new() { Id = InProgress, Name = "In Progress", Color = "#fef3c7", IsBuiltIn = true },
		new() { Id = Done,       Name = "Done",        Color = "#d1fae5", IsBuiltIn = true },
		new() { Id = Abandoned,  Name = "Abandoned",   Color = "#e5e7eb", IsBuiltIn = true },
		new() { Id = Archived,   Name = "Archived",    Color = "#e5e7eb", IsBuiltIn = true },
	};
}
