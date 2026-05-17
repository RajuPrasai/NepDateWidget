using System.IO;

namespace NepDateWidget.Helpers;

/// <summary>
/// Atomic file write helper. Mirrors the pattern used by
/// <c>SettingsService.Save()</c>: write to <c>.tmp</c>, then atomically
/// swap with <c>File.Replace</c> (or <c>File.Move</c> on first write).
/// Cleans up the resulting <c>.bak</c>. Best-effort; never throws.
/// </summary>
internal static class AtomicFile
{
    public static bool WriteAllText(string path, string contents)
    {
        try
        {
            var tmp = path + ".tmp";
            var bak = path + ".bak";
            File.WriteAllText(tmp, contents);

            if (File.Exists(path))
            {
                File.Replace(tmp, path, bak);
                try { if (File.Exists(bak)) { File.Delete(bak); } } catch { }
            }
            else
            {
                File.Move(tmp, path);
            }
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }
}
