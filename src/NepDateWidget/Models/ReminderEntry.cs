using System.Text.Json;
using System.Text.Json.Serialization;

namespace NepDateWidget.Models;

public enum ReminderRecurrence
{
    None,
    Daily,
    Weekly,
    Monthly,
    Yearly,
}

public sealed class ReminderEntry
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Title { get; set; } = string.Empty;
    public string Notes { get; set; } = string.Empty;

    // BS date string "YYYY/MM/DD" (advances for recurring reminders)
    public string BsDate { get; set; } = string.Empty;

    // Original BS date string "YYYY/MM/DD" at creation (never mutated, used for recurrence calculation)
    public string OriginalBsDate { get; set; } = string.Empty;

    // Time of day (HH:mm)
    public string Time { get; set; } = "09:00";

    public ReminderRecurrence Recurrence { get; set; } = ReminderRecurrence.None;

    // Optional end date for recurring reminders (BS date string "YYYY/MM/DD", null = indefinite)
    public string? EndDate { get; set; }

    public bool IsCompleted { get; set; }

    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
    public DateTime? LastFiredUtc { get; set; }

    // Captures old integer fields from legacy JSON for migration
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? ExtensionData { get; set; }

    public static (int Year, int Month, int Day)? ParseDate(string? date)
    {
        if (string.IsNullOrEmpty(date))
        {
            return null;
        }

        var parts = date.Split('/', '-');
        if (parts.Length != 3)
        {
            return null;
        }

        if (!int.TryParse(parts[0], out int y) ||
            !int.TryParse(parts[1], out int m) ||
            !int.TryParse(parts[2], out int d))
        {
            return null;
        }

        return (y, m, d);
    }

    public static string FormatDate(int year, int month, int day) => $"{year}/{month:D2}/{day:D2}";

    // Called after deserialization to migrate old 6-int format to 2-string format
    internal void MigrateFromLegacyIfNeeded()
    {
        if (!string.IsNullOrEmpty(BsDate) || ExtensionData is null)
        {
            return;
        }

        if (ExtensionData.TryGetValue("BsYear", out var y) &&
            ExtensionData.TryGetValue("BsMonth", out var m) &&
            ExtensionData.TryGetValue("BsDay", out var d))
        {
            BsDate = FormatDate(y.GetInt32(), m.GetInt32(), d.GetInt32());
        }

        if (ExtensionData.TryGetValue("OriginalBsYear", out var oy) &&
            ExtensionData.TryGetValue("OriginalBsMonth", out var om) &&
            ExtensionData.TryGetValue("OriginalBsDay", out var od) &&
            oy.GetInt32() > 0)
        {
            OriginalBsDate = FormatDate(oy.GetInt32(), om.GetInt32(), od.GetInt32());
        }
        else if (!string.IsNullOrEmpty(BsDate))
        {
            OriginalBsDate = BsDate;
        }

        ExtensionData = null;
    }
}
