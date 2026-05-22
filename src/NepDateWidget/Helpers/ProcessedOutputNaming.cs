using NepDate;
using System.IO;

namespace NepDateWidget.Helpers;

/// <summary>
/// Builds output paths for processed files using the pattern:
/// {baseName}_Processed_{YYYY_MM_DD}.{ext}
/// Appends _2, _3, ... on collision.
/// </summary>
public static class ProcessedOutputNaming
{
    /// <summary>
    /// Builds an output path using today's BS date from the real system clock.
    /// </summary>
    public static string BuildOutputPath(string inputPath, string outputDir, string targetExtension)
    {
        var today = NepaliDate.Today;
        return BuildOutputPath(inputPath, outputDir, targetExtension,
            () => (today.Year, today.Month, today.Day));
    }

    /// <summary>
    /// Builds an output path using a date source delegate.
    /// The delegate returns (bsYear, bsMonth, bsDay).
    /// Overload used by tests to inject a fixed date without real clock dependency.
    /// </summary>
    public static string BuildOutputPath(
        string inputPath,
        string outputDir,
        string targetExtension,
        Func<(int year, int month, int day)> getTodayBsDate)
    {
        var baseName = Path.GetFileNameWithoutExtension(inputPath);
        var (year, month, day) = getTodayBsDate();
        var dateSuffix = $"{year:D4}_{month:D2}_{day:D2}";
        var ext = targetExtension.TrimStart('.').ToLowerInvariant();

        // Build candidate path; check for collision.
        var candidate = Path.Combine(outputDir, $"{baseName}_Processed_{dateSuffix}.{ext}");
        if (!File.Exists(candidate))
            return candidate;

        // Collision: append _2, _3, ...
        for (int counter = 2; counter <= 99; counter++)
        {
            candidate = Path.Combine(outputDir, $"{baseName}_Processed_{dateSuffix}_{counter}.{ext}");
            if (!File.Exists(candidate))
                return candidate;
        }

        // Failsafe: all _2.._99 exist (pathological case).
        return Path.Combine(outputDir, $"{baseName}_Processed_{dateSuffix}_dup.{ext}");
    }
}
