namespace NepDateWidget.Services;

/// <summary>
/// Thin abstraction over <see cref="System.Windows.Clipboard"/> so view models
/// can be tested without spinning up a WPF dispatcher. The default
/// implementation (<see cref="ClipboardService"/>) wraps the static API and
/// swallows the well-known <c>OpenClipboard failed</c> exception that Windows
/// throws when another process briefly holds the clipboard.
/// </summary>
public interface IClipboardService
{
    /// <summary>
    /// Places <paramref name="text"/> on the system clipboard. Returns true on
    /// success, false on no-op (null/empty) or failure. Implementations must
    /// not throw.
    /// </summary>
    bool SetText(string? text);
}
