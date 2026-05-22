using System.Runtime.InteropServices;

namespace NepDateWidget.Helpers;

/// <summary>
/// Runtime distribution-channel detection.
/// Separates unpackaged (dev/portable) builds from MSIX-packaged (Store or sideloaded).
/// Used to pick channel-appropriate data paths, mutex names, and autostart mechanisms.
/// </summary>
internal static class AppEnvironment
{
    // GetCurrentPackageFullName returns this value when the process has no MSIX package identity.
    private const int ErrorAppModelNoPackage = 15700;

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
    private static extern int GetCurrentPackageFullName(
        ref int packageFullNameLength, [Out] char[]? packageFullName);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
    private static extern int GetCurrentPackageFamilyName(
        ref int familyNameLength, [Out] char[]? familyName);

    private static readonly Lazy<bool> _isPackaged = new(static () =>
    {
        int length = 0;
        return GetCurrentPackageFullName(ref length, null) != ErrorAppModelNoPackage;
    });

    private static readonly Lazy<string?> _packageFamilyName = new(static () =>
    {
        int length = 0;
        if (GetCurrentPackageFamilyName(ref length, null) == ErrorAppModelNoPackage || length == 0)
        {
            return null;
        }

        var buf = new char[length];
        return GetCurrentPackageFamilyName(ref length, buf) == 0
            ? new string(buf, 0, length - 1) // length includes null terminator
            : null;
    });

    /// <summary>
    /// True when the process is running inside an MSIX package (Store or sideloaded).
    /// False for unpackaged (dev/portable) builds.
    /// </summary>
    public static bool IsPackaged => _isPackaged.Value;

    /// <summary>
    /// The MSIX package family name (e.g., <c>Publisher.AppName_publisherhash</c>), or null
    /// when running unpackaged. Stable across app version updates; does not include version
    /// or architecture. Computed via GetCurrentPackageFamilyName (kernel32).
    /// </summary>
    internal static string? PackageFamilyName => _packageFamilyName.Value;

    /// <summary>
    /// Single-instance mutex name scoped to the distribution channel. Prevents the
    /// unpackaged and Store instances from blocking each other when both are installed
    /// on the same machine simultaneously.
    /// </summary>
    public static string SingleInstanceMutexName =>
        IsPackaged ? "NepDateWidget_SingleInstance_Store_v1" : "NepDateWidget_SingleInstance_v1";

    /// <summary>
    /// %LOCALAPPDATA% subfolder name for user data. The Store channel uses a separate
    /// root from the unpackaged channel so uninstalling one cannot affect the other's data.
    /// </summary>
    public static string DataFolderName =>
        IsPackaged ? "NepDateWidget.Store" : "NepDateWidget";
}
