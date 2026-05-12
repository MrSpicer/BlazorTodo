namespace TodoList.Models;

public static class BuiltInPriorityIds
{
	public static readonly Guid Low       = new("00000001-0000-0000-0000-000000000002");
	public static readonly Guid Medium    = new("00000002-0000-0000-0000-000000000002");
	public static readonly Guid High      = new("00000003-0000-0000-0000-000000000002");
	public static readonly Guid Emergency = new("00000004-0000-0000-0000-000000000002");

	public static readonly IReadOnlyList<Guid> AllIds = new[]
	{
		Low, Medium, High, Emergency
	};

	public static bool IsBuiltIn(Guid id) => AllIds.Contains(id);

	public static Guid FromLegacyEnum(int legacyValue) => legacyValue switch
	{
		0 => Low,
		1 => Medium,
		2 => High,
		3 => Emergency,
		_ => Medium
	};

	public static int NaturalOrder(Guid id)
	{
		if (id == Low) return 1;
		if (id == Medium) return 2;
		if (id == High) return 3;
		if (id == Emergency) return 4;
		return int.MaxValue;
	}

	public static IReadOnlyList<Priority> Seed() => new List<Priority>
	{
		new() { Id = Low,       Name = "Low",       Color = "#22c55e", Rank = 1, IsBuiltIn = true },
		new() { Id = Medium,    Name = "Medium",    Color = "#3b82f6", Rank = 2, IsBuiltIn = true },
		new() { Id = High,      Name = "High",      Color = "#f97316", Rank = 3, IsBuiltIn = true },
		new() { Id = Emergency, Name = "Emergency", Color = "#ef4444", Rank = 4, IsBuiltIn = true },
	};
}
