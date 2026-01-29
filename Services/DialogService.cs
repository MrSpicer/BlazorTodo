using Microsoft.JSInterop;

namespace TodoList.Services;

/// <summary>
/// Implementation of IDialogService using JavaScript interop.
/// </summary>
public class DialogService : IDialogService
{
    private readonly IJSRuntime _jsRuntime;

    public DialogService(IJSRuntime jsRuntime)
    {
        _jsRuntime = jsRuntime;
    }

    public async Task<bool> ConfirmAsync(string message, string title = "Confirm")
    {
        return await _jsRuntime.InvokeAsync<bool>("confirm", message);
    }

    public async Task AlertAsync(string message, string title = "Alert")
    {
        await _jsRuntime.InvokeVoidAsync("alert", message);
    }
}
