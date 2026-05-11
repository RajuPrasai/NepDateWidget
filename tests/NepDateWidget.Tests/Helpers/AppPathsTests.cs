using System.IO;
using NepDateWidget.Helpers;

namespace NepDateWidget.Tests.Helpers;

/// <summary>
/// Smoke tests for <see cref="AppPaths"/>. The class uses <see cref="Lazy{T}"/>
/// to resolve once per process, so we cannot exercise the portable-vs-installed
/// branches in isolation here. We assert the contracts that callers depend on:
/// every emitted path is rooted in <see cref="AppPaths.DataDirectory"/>, the
/// directory exists, and the file names match what the rest of the app reads.
/// </summary>
public class AppPathsTests
{
    [Fact]
    public void DataDirectory_Exists_AfterFirstAccess()
    {
        Assert.True(Directory.Exists(AppPaths.DataDirectory));
    }

    [Fact]
    public void DataDirectory_EndsWithAppDataSubfolder()
    {
        // Both portable and installed mode put data under "AppData".
        // Velopack owns the un-suffixed install root in installed mode.
        Assert.EndsWith(
            AppPaths.DataSubfolder,
            AppPaths.DataDirectory.TrimEnd(Path.DirectorySeparatorChar),
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void SettingsPath_IsUnderConfigDirectory_AndNamedSettingsJson()
    {
        Assert.Equal(
            Path.Combine(AppPaths.DataDirectory, "config", "settings.json"),
            AppPaths.SettingsPath);
    }

    [Fact]
    public void RemindersPath_IsUnderDataSubdir_AndNamedRemindersJson()
    {
        Assert.Equal(
            Path.Combine(AppPaths.DataDirectory, "data", "reminders.json"),
            AppPaths.RemindersPath);
    }

    [Fact]
    public void NotesPath_IsUnderDataSubdir_AndNamedNotesJson()
    {
        Assert.Equal(
            Path.Combine(AppPaths.DataDirectory, "data", "notes.json"),
            AppPaths.NotesPath);
    }

    [Fact]
    public void LogPath_IsUnderDataDirectory_AndNamedNepDateLog()
    {
        Assert.Equal(
            Path.Combine(AppPaths.DataDirectory, "nepdate.log"),
            AppPaths.LogPath);
    }

    [Fact]
    public void ExeDirectory_IsAbsolute_AndExists()
    {
        Assert.True(Path.IsPathRooted(AppPaths.ExeDirectory));
        Assert.True(Directory.Exists(AppPaths.ExeDirectory));
    }

    [Fact]
    public void Constants_HaveExpectedValues()
    {
        // These are part of the published contract: portable detection looks
        // for "portable.flag", and the installed folder name must match
        // Velopack's pack id "NepDateWidget".
        Assert.Equal("portable.flag", AppPaths.PortableFlagFile);
        Assert.Equal("NepDateWidget", AppPaths.InstalledFolderName);
        Assert.Equal("AppData", AppPaths.DataSubfolder);
    }

    [Fact]
    public void MigrateLegacyData_DoesNotThrow_OnRepeatedInvocation()
    {
        // Idempotent and best-effort. Must never throw, even when the source
        // files do not exist.
        var ex = Record.Exception(() =>
        {
            AppPaths.MigrateLegacyData();
            AppPaths.MigrateLegacyData();
        });
        Assert.Null(ex);
    }

    [Fact]
    public void ShortcutsPath_IsUnderConfigDirectory_AndNamedShortcutsJson()
    {
        Assert.Equal(
            Path.Combine(AppPaths.DataDirectory, "config", "shortcuts.json"),
            AppPaths.ShortcutsPath);
    }

    [Fact]
    public void DocumentsPath_IsUnderDataSubdir_AndNamedDocumentsJson()
    {
        Assert.Equal(
            Path.Combine(AppPaths.DataDirectory, "data", "documents.json"),
            AppPaths.DocumentsPath);
    }

    [Fact]
    public void RunHistoryPath_IsUnderDataSubdir_AndNamedRunHistoryJson()
    {
        Assert.Equal(
            Path.Combine(AppPaths.DataDirectory, "data", "run-history.json"),
            AppPaths.RunHistoryPath);
    }

    [Fact]
    public void ScriptsPath_IsUnderConfigDirectory_AndNamedScriptsJson()
    {
        Assert.Equal(
            Path.Combine(AppPaths.DataDirectory, "config", "scripts.json"),
            AppPaths.ScriptsPath);
    }

    [Fact]
    public void AppStatePath_IsUnderDataDirectory_AndNamedRuntimeJson()
    {
        Assert.Equal(
            Path.Combine(AppPaths.DataDirectory, "runtime.json"),
            AppPaths.AppStatePath);
    }

    [Fact]
    public void LocalizationPath_IsUnderConfigDirectory_AndNamedLocalizationJson()
    {
        Assert.Equal(
            Path.Combine(AppPaths.DataDirectory, "config", "localization.json"),
            AppPaths.LocalizationPath);
    }
}
