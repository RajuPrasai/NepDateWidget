using NepDateWidget.Models;
using System.Text.Json;

namespace NepDateWidget.Tests;

public sealed class ReminderEntryTests
{
    // ── ParseDate ─────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("2082/06/15", 2082, 6, 15)]
    [InlineData("2082-06-15", 2082, 6, 15)]
    [InlineData("2082/01/01", 2082, 1, 1)]
    public void ParseDate_ValidFormats_ReturnsTuple(string input, int y, int m, int d)
    {
        var result = ReminderEntry.ParseDate(input);
        Assert.NotNull(result);
        Assert.Equal((y, m, d), result.Value);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("2082")]
    [InlineData("2082/06")]
    [InlineData("abc/def/ghi")]
    [InlineData("2082/06/15/01")]
    public void ParseDate_InvalidInput_ReturnsNull(string? input)
    {
        Assert.Null(ReminderEntry.ParseDate(input));
    }

    // ── FormatDate ────────────────────────────────────────────────────────────

    [Fact]
    public void FormatDate_ZeroPads()
    {
        Assert.Equal("2082/01/05", ReminderEntry.FormatDate(2082, 1, 5));
    }

    [Fact]
    public void FormatDate_TwoDigitMonth()
    {
        Assert.Equal("2082/12/20", ReminderEntry.FormatDate(2082, 12, 20));
    }

    // ── MigrateFromLegacyIfNeeded ─────────────────────────────────────────────

    [Fact]
    public void MigrateFromLegacy_WithExtensionData_SetsBsDate()
    {
        var entry = new ReminderEntry
        {
            BsDate = "",
            ExtensionData = new Dictionary<string, JsonElement>
            {
                ["BsYear"] = JsonDocument.Parse("2082").RootElement,
                ["BsMonth"] = JsonDocument.Parse("6").RootElement,
                ["BsDay"] = JsonDocument.Parse("15").RootElement,
            }
        };

        entry.MigrateFromLegacyIfNeeded();

        Assert.Equal("2082/06/15", entry.BsDate);
        Assert.Equal("2082/06/15", entry.OriginalBsDate);
        Assert.Null(entry.ExtensionData);
    }

    [Fact]
    public void MigrateFromLegacy_WithOriginalFields_SetsOriginalBsDate()
    {
        var entry = new ReminderEntry
        {
            BsDate = "",
            ExtensionData = new Dictionary<string, JsonElement>
            {
                ["BsYear"] = JsonDocument.Parse("2082").RootElement,
                ["BsMonth"] = JsonDocument.Parse("6").RootElement,
                ["BsDay"] = JsonDocument.Parse("15").RootElement,
                ["OriginalBsYear"] = JsonDocument.Parse("2082").RootElement,
                ["OriginalBsMonth"] = JsonDocument.Parse("5").RootElement,
                ["OriginalBsDay"] = JsonDocument.Parse("10").RootElement,
            }
        };

        entry.MigrateFromLegacyIfNeeded();

        Assert.Equal("2082/06/15", entry.BsDate);
        Assert.Equal("2082/05/10", entry.OriginalBsDate);
    }

    [Fact]
    public void MigrateFromLegacy_AlreadyHasBsDate_NoOp()
    {
        var entry = new ReminderEntry
        {
            BsDate = "2082/01/01",
            ExtensionData = new Dictionary<string, JsonElement>
            {
                ["BsYear"] = JsonDocument.Parse("9999").RootElement,
                ["BsMonth"] = JsonDocument.Parse("1").RootElement,
                ["BsDay"] = JsonDocument.Parse("1").RootElement,
            }
        };

        entry.MigrateFromLegacyIfNeeded();

        Assert.Equal("2082/01/01", entry.BsDate); // unchanged
    }

    [Fact]
    public void MigrateFromLegacy_NullExtensionData_NoOp()
    {
        var entry = new ReminderEntry { BsDate = "", ExtensionData = null };
        entry.MigrateFromLegacyIfNeeded();
        Assert.Equal("", entry.BsDate);
    }

    [Fact]
    public void MigrateFromLegacy_MissingBsYearKey_NoOp()
    {
        var entry = new ReminderEntry
        {
            BsDate = "",
            ExtensionData = new Dictionary<string, JsonElement>
            {
                ["SomeOtherField"] = JsonDocument.Parse("42").RootElement,
            }
        };

        entry.MigrateFromLegacyIfNeeded();
        Assert.Equal("", entry.BsDate);
    }

    [Fact]
    public void MigrateFromLegacy_OriginalYearZero_FallsBackToBsDate()
    {
        var entry = new ReminderEntry
        {
            BsDate = "",
            ExtensionData = new Dictionary<string, JsonElement>
            {
                ["BsYear"] = JsonDocument.Parse("2082").RootElement,
                ["BsMonth"] = JsonDocument.Parse("6").RootElement,
                ["BsDay"] = JsonDocument.Parse("15").RootElement,
                ["OriginalBsYear"] = JsonDocument.Parse("0").RootElement,
                ["OriginalBsMonth"] = JsonDocument.Parse("0").RootElement,
                ["OriginalBsDay"] = JsonDocument.Parse("0").RootElement,
            }
        };

        entry.MigrateFromLegacyIfNeeded();

        Assert.Equal("2082/06/15", entry.BsDate);
        Assert.Equal("2082/06/15", entry.OriginalBsDate); // falls back to BsDate
    }

    // ── Default values ────────────────────────────────────────────────────────

    [Fact]
    public void NewEntry_HasUniqueId()
    {
        var a = new ReminderEntry();
        var b = new ReminderEntry();
        Assert.NotEqual(a.Id, b.Id);
        Assert.Equal(32, a.Id.Length); // Guid.ToString("N") = 32 hex chars
    }

    [Fact]
    public void NewEntry_DefaultValues()
    {
        var entry = new ReminderEntry();
        Assert.Equal(string.Empty, entry.Title);
        Assert.Equal(string.Empty, entry.Notes);
        Assert.Equal("09:00", entry.Time);
        Assert.Equal(ReminderRecurrence.None, entry.Recurrence);
        Assert.False(entry.IsCompleted);
        Assert.Null(entry.EndDate);
    }
}
