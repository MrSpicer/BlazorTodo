namespace TodoList.Models.Enums;

/// <summary>
/// Legacy fixed-set priority enum. Kept for back-compat deserialization of pre-entity-Priority
/// data; not authoritative — use <see cref="TodoList.Models.TodoItem.PriorityId"/>.
/// </summary>
public enum LegacyPriority
{
	Low = 0,
	Medium = 1,
	High = 2,
	Emergency = 3
}
