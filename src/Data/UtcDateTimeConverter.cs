using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace TodoList.Data;

/// <summary>
/// Normalizes every DateTime to UTC at the persistence boundary. Npgsql refuses to write
/// Kind=Local/Unspecified values to 'timestamp with time zone' columns, and dates that
/// round-trip through JSON with a numeric offset (old exports, pre-account localStorage
/// data read back by the migration path) arrive as Kind=Local. Local values are converted
/// to the same instant in UTC; Unspecified values are assumed to already be UTC.
/// </summary>
public class UtcDateTimeConverter : ValueConverter<DateTime, DateTime>
{
	public UtcDateTimeConverter()
		: base(v => ToUtc(v), v => DateTime.SpecifyKind(v, DateTimeKind.Utc))
	{
	}

	internal static DateTime ToUtc(DateTime value) => value.Kind switch
	{
		DateTimeKind.Utc => value,
		DateTimeKind.Local => value.ToUniversalTime(),
		_ => DateTime.SpecifyKind(value, DateTimeKind.Utc),
	};
}

public class NullableUtcDateTimeConverter : ValueConverter<DateTime?, DateTime?>
{
	public NullableUtcDateTimeConverter()
		: base(
			v => v.HasValue ? UtcDateTimeConverter.ToUtc(v.Value) : v,
			v => v.HasValue ? DateTime.SpecifyKind(v.Value, DateTimeKind.Utc) : v)
	{
	}
}
