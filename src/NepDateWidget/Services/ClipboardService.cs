using NepDateWidget.Helpers;

namespace NepDateWidget.Services;

/// <summary>
/// Default <see cref="IClipboardService"/> backed by WPF's static
/// <see cref="System.Windows.Clipboard"/>. Errors are logged and absorbed so a
/// transient clipboard contention never crashes the widget.
/// </summary>
public sealed class ClipboardService : IClipboardService
{
    public bool SetText(string? text)
    {
        if (string.IsNullOrEmpty(text)) return false;
        try
        {
            System.Windows.Clipboard.SetText(text);
            return true;
        }
        catch (Exception ex)
        {
            Log.Error($"clipboard set failed: {ex.Message}");
            return false;
        }
    }
}
