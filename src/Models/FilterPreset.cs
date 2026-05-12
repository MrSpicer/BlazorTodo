using System.Text.Json;
using System.Text.Json.Serialization;
using TodoList.Models.Enums;

namespace TodoList.Models;

public record FilterPreset(
	string Name,
	string SearchText,
	[property: JsonConverter(typeof(LegacyPriorityIdListConverter))]
	List<Guid> SelectedPriorities,
	List<Guid> SelectedStatuses,
	List<SortCriterion> SortCriteria)
{
	public const string DefaultName = "Default";

	public static FilterPreset SystemDefault => new(
		DefaultName,
		string.Empty,
		new List<Guid>(),
		BuiltInStatusIds.DefaultFilterIds.ToList(),
		new List<SortCriterion>
		{
			new(SortOption.Status, true),
			new(SortOption.Priority, true),
		});

	public static FilterPreset FromCriteria(string name, TodoFilterCriteria criteria) => new(
		name,
		criteria.SearchText,
		criteria.SelectedPriorities.ToList(),
		criteria.SelectedStatuses.ToList(),
		criteria.SortCriteria.ToList());
}

/// <summary>
/// Reads a list of priority ids that may have been persisted under the old enum-int format
/// (e.g. <c>[2, 3]</c>) and remaps each int through <see cref="BuiltInPriorityIds.FromLegacyEnum"/>.
/// Guid-string elements pass through unchanged. Writes always emit Guid strings, so re-saving
/// a preset migrates its on-disk form forward.
/// </summary>
public class LegacyPriorityIdListConverter : JsonConverter<List<Guid>>
{
	public override List<Guid> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
	{
		var list = new List<Guid>();
		if (reader.TokenType == JsonTokenType.Null)
			return list;
		if (reader.TokenType != JsonTokenType.StartArray)
			throw new JsonException("Expected start of array");

		while (reader.Read())
		{
			switch (reader.TokenType)
			{
				case JsonTokenType.EndArray:
					return list;
				case JsonTokenType.Number when reader.TryGetInt32(out var legacy):
					list.Add(BuiltInPriorityIds.FromLegacyEnum(legacy));
					break;
				case JsonTokenType.String:
					if (reader.TryGetGuid(out var g))
						list.Add(g);
					break;
			}
		}
		throw new JsonException("Unterminated array");
	}

	public override void Write(Utf8JsonWriter writer, List<Guid> value, JsonSerializerOptions options)
	{
		writer.WriteStartArray();
		foreach (var g in value)
			writer.WriteStringValue(g);
		writer.WriteEndArray();
	}
}
