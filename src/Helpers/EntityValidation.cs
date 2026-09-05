using System.ComponentModel.DataAnnotations;

namespace TodoList.Helpers;

/// <summary>
/// DataAnnotations validation shared by every persistence boundary. Entity <c>IsValid()</c>
/// checks only identity/required fields; this additionally enforces every annotation
/// (max lengths, hex-colour regex, etc.) so no code path — form, import, migration, or a
/// programmatic convert — can persist a malformed entity to either backend.
/// </summary>
public static class EntityValidation
{
	public static bool Passes<T>(T entity) where T : class => TryValidate(entity, out _);

	/// <summary>
	/// Validates <paramref name="entity"/> and hands back the first annotation message, so
	/// callers can tell the user which field actually failed rather than guessing.
	/// </summary>
	public static bool TryValidate<T>(T entity, out string? firstError) where T : class
	{
		var results = new List<ValidationResult>();
		var ok = Validator.TryValidateObject(
			entity, new ValidationContext(entity), results, validateAllProperties: true);
		firstError = ok ? null : results.FirstOrDefault()?.ErrorMessage;
		return ok;
	}
}
