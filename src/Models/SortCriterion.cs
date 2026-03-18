using TodoList.Models.Enums;

namespace TodoList.Models;

public record struct SortCriterion(SortOption Option, bool Descending);
