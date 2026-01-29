using Microsoft.JSInterop;

namespace TodoList.Services;

/// <summary>
/// Service for file operations using JavaScript interop.
/// </summary>
public interface IFileService
{
    /// <summary>
    /// Triggers a file download in the browser.
    /// </summary>
    Task DownloadFileAsync(string fileName, string content, string contentType = "application/json");
}

/// <summary>
/// Implementation of IFileService using JavaScript interop.
/// </summary>
public class FileService : IFileService
{
    private readonly IJSRuntime _jsRuntime;

    public FileService(IJSRuntime jsRuntime)
    {
        _jsRuntime = jsRuntime;
    }

    public async Task DownloadFileAsync(string fileName, string content, string contentType = "application/json")
    {
        // Convert content to base64 for safe transfer
        var bytes = System.Text.Encoding.UTF8.GetBytes(content);
        var base64 = Convert.ToBase64String(bytes);

        await _jsRuntime.InvokeVoidAsync("downloadFile", fileName, base64, contentType);
    }
}
