using System.Windows;

namespace NepDateWidget.Helpers;

internal static class WindowHelpers
{
    /// <summary>
    /// Returns true if any application-owned top-level window other than <paramref name="excluding"/>
    /// is currently the active OS foreground window. Used by transient popups to decide
    /// whether a Deactivated event represents a real focus loss to another app or just
    /// a click on a sibling window owned by this same app (pill / shell).
    /// </summary>
    public static bool IsAnyAppWindowActive(Window? excluding = null)
    {
        var app = Application.Current;
        if (app is null) return false;

        foreach (Window w in app.Windows)
        {
            if (ReferenceEquals(w, excluding)) continue;
            if (w.IsActive) return true;
        }
        return false;
    }
}
