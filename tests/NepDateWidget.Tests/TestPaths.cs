namespace NepDateWidget.Tests;

/// <summary>
/// Resolves paths to the shipped default config files at test runtime.
/// Files are copied to the test output directory via Content items in the test project's csproj.
/// </summary>
internal static class TestPaths
{
    private static string DefaultsDir => Path.Combine(AppContext.BaseDirectory, "Resources", "configs");

    public static string DefaultLocalizationPath => Path.Combine(DefaultsDir, "localization.json");
    public static string DefaultShortcutsPath    => Path.Combine(DefaultsDir, "shortcuts.json");
    public static string DefaultRunHistoryPath   => Path.Combine(DefaultsDir, "run-history.json");
    public static string DefaultSettingsPath     => Path.Combine(DefaultsDir, "settings.json");
    public static string DefaultScriptsPath      => Path.Combine(DefaultsDir, "scripts.json");
}
