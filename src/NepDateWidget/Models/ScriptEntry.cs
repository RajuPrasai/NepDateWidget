namespace NepDateWidget.Models;

/// <summary>
/// Represents a user-defined script command loaded from scripts.json.
/// Supported interpreter values: powershell | pwsh | cmd | python | wsl
/// </summary>
public sealed class ScriptEntry
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public string Interpreter { get; set; } = "powershell";
}
