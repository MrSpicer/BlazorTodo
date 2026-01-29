namespace TodoList.Services;

/// <summary>
/// Service for displaying dialog prompts to the user.
/// </summary>
public interface IDialogService
{
    /// <summary>
    /// Shows a confirmation dialog and returns the user's response.
    /// </summary>
    /// <param name="message">The message to display.</param>
    /// <param name="title">Optional title for the dialog.</param>
    /// <returns>True if user confirmed, false otherwise.</returns>
    Task<bool> ConfirmAsync(string message, string title = "Confirm");

    /// <summary>
    /// Shows an alert dialog to the user.
    /// </summary>
    /// <param name="message">The message to display.</param>
    /// <param name="title">Optional title for the dialog.</param>
    Task AlertAsync(string message, string title = "Alert");
}
