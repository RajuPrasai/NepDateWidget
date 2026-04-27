namespace NepDateWidget.Models;

/// <summary>
/// Result of an AD↔BS or BS↔AD conversion.
/// Check <see cref="IsSuccess"/> before reading <see cref="Result"/>.
/// </summary>
public sealed class ConversionResult
{
    // ── Success factory ───────────────────────────────────────────────────────

    public static ConversionResult Ok(string result, string resultLong = "")
        => new() { IsSuccess = true, Result = result, ResultLong = resultLong };

    // ── Error factory ─────────────────────────────────────────────────────────

    public static ConversionResult Fail(string errorMessage)
        => new() { IsSuccess = false, ErrorMessage = errorMessage };

    // ── Properties ────────────────────────────────────────────────────────────

    public bool IsSuccess { get; private init; }

    /// <summary>Short formatted result (e.g. "2082/12/20" or "2026/04/03").</summary>
    public string Result { get; private init; } = string.Empty;

    /// <summary>Long formatted result (e.g. "Chaitra 20, 2082").</summary>
    public string ResultLong { get; private init; } = string.Empty;

    /// <summary>User-facing error message when <see cref="IsSuccess"/> is false.</summary>
    public string ErrorMessage { get; private init; } = string.Empty;
}
