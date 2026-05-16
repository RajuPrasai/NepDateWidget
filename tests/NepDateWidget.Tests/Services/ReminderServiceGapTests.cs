using NepDateWidget.Models;
using NepDateWidget.Services;
using NepDateWidget.Tests.Services;

namespace NepDateWidget.Tests.Services;

/// <summary>
/// Additional ReminderService tests covering gaps identified in audit:
/// Update event/edge cases, GetRecurringForDate, monthly recurrence, EndDate expiration.
/// </summary>
public sealed class ReminderServiceGapTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string _filePath;
    private readonly FakeNepaliDateAdapter _adapter;

    public ReminderServiceGapTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"NepDateWidget_Tests_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
        _filePath = Path.Combine(_tempDir, "reminders.json");
        _adapter = new FakeNepaliDateAdapter();
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, true); } catch { }
    }

    private ReminderService CreateService()
    {
        var svc = new ReminderService(_filePath, _adapter);
        svc.Load();
        return svc;
    }

    // ── Update ────────────────────────────────────────────────────────────────

    [Fact]
    public void Update_RaisesRemindersChanged()
    {
        var svc = CreateService();
        var entry = new ReminderEntry { Title = "X", BsDate = "2082/01/01", Time = "09:00" };
        svc.Add(entry);

        int called = 0;
        svc.RemindersChanged += (_, _) => called++;

        entry.Title = "Updated";
        svc.Update(entry);
        Assert.Equal(1, called);
    }

    [Fact]
    public void Update_NullEntry_NoOp()
    {
        var svc = CreateService();
        svc.Add(new ReminderEntry { Title = "X", BsDate = "2082/01/01" });

        int called = 0;
        svc.RemindersChanged += (_, _) => called++;
        svc.Update(null!);
        Assert.Equal(0, called);
    }

    [Fact]
    public void Update_NonexistentId_NoOp()
    {
        var svc = CreateService();
        svc.Add(new ReminderEntry { Title = "X", BsDate = "2082/01/01" });

        int called = 0;
        svc.RemindersChanged += (_, _) => called++;
        svc.Update(new ReminderEntry { Id = "nonexistent", Title = "Y", BsDate = "2082/01/01" });
        Assert.Equal(0, called);
    }

    [Fact]
    public void Update_TruncatesNotesAt500()
    {
        var svc = CreateService();
        var entry = new ReminderEntry { Title = "X", BsDate = "2082/01/01" };
        svc.Add(entry);

        entry.Notes = new string('B', 600);
        svc.Update(entry);

        Assert.Equal(500, svc.GetAll()[0].Notes.Length);
    }

    [Fact]
    public void Update_ChangedBsDate_UpdatesOriginalBsDate()
    {
        var svc = CreateService();
        var entry = new ReminderEntry { Title = "X", BsDate = "2082/01/01" };
        svc.Add(entry);
        Assert.Equal("2082/01/01", svc.GetAll()[0].OriginalBsDate);

        // Create a new entry object to simulate the edit flow (different reference)
        var updated = new ReminderEntry
        {
            Id = entry.Id,
            Title = "X",
            BsDate = "2082/02/15",
            OriginalBsDate = entry.OriginalBsDate,
        };
        svc.Update(updated);

        Assert.Equal("2082/02/15", svc.GetAll()[0].OriginalBsDate);
    }

    [Fact]
    public void Update_SameBsDate_KeepsOriginalBsDate()
    {
        var svc = CreateService();
        var entry = new ReminderEntry { Title = "X", BsDate = "2082/01/01" };
        svc.Add(entry);

        var updated = new ReminderEntry
        {
            Id = entry.Id,
            Title = "Updated",
            BsDate = "2082/01/01",
            OriginalBsDate = entry.OriginalBsDate,
        };
        svc.Update(updated);

        Assert.Equal("2082/01/01", svc.GetAll()[0].OriginalBsDate);
    }

    // ── GetRecurringForDate ───────────────────────────────────────────────────

    [Fact]
    public void GetRecurringForDate_DailyReminder_ReturnsOnLaterDate()
    {
        var svc = CreateService();
        svc.Add(new ReminderEntry
        {
            Title = "Daily",
            BsDate = "2082/12/10",
            Recurrence = ReminderRecurrence.Daily,
            Time = "09:00",
        });

        var results = svc.GetRecurringForDate(2082, 12, 15);
        Assert.Single(results);
        Assert.Equal("Daily", results[0].Title);
    }

    [Fact]
    public void GetRecurringForDate_ExcludesExactDateMatch()
    {
        var svc = CreateService();
        svc.Add(new ReminderEntry
        {
            Title = "Daily",
            BsDate = "2082/12/10",
            Recurrence = ReminderRecurrence.Daily,
            Time = "09:00",
        });

        // Exact date match should be excluded (those are returned by GetForDate)
        var results = svc.GetRecurringForDate(2082, 12, 10);
        Assert.Empty(results);
    }

    [Fact]
    public void GetRecurringForDate_WeeklyReminder_ReturnsOnCorrectDay()
    {
        var svc = CreateService();
        svc.Add(new ReminderEntry
        {
            Title = "Weekly",
            BsDate = "2082/12/10",
            Recurrence = ReminderRecurrence.Weekly,
            Time = "09:00",
        });

        // 7 days later
        var results = svc.GetRecurringForDate(2082, 12, 17);
        Assert.Single(results);

        // Non-week offset: should not match
        var noResults = svc.GetRecurringForDate(2082, 12, 12);
        Assert.Empty(noResults);
    }

    [Fact]
    public void GetRecurringForDate_ExcludesCompleted()
    {
        var svc = CreateService();
        svc.Add(new ReminderEntry
        {
            Title = "Done",
            BsDate = "2082/12/10",
            Recurrence = ReminderRecurrence.Daily,
            Time = "09:00",
            IsCompleted = true,
        });

        var results = svc.GetRecurringForDate(2082, 12, 15);
        Assert.Empty(results);
    }

    [Fact]
    public void GetRecurringForDate_ExcludesNonRecurring()
    {
        var svc = CreateService();
        svc.Add(new ReminderEntry
        {
            Title = "OneShot",
            BsDate = "2082/12/10",
            Recurrence = ReminderRecurrence.None,
            Time = "09:00",
        });

        var results = svc.GetRecurringForDate(2082, 12, 15);
        Assert.Empty(results);
    }

    // ── HasRemindersForDateExpanded with monthly ──────────────────────────────

    [Fact]
    public void HasRemindersForDateExpanded_Monthly_ReturnsTrueOnMonthlyDate()
    {
        var svc = CreateService();
        // Monthly from 2082/06/10. WouldRecurOnDate uses O(1) math:
        // expected day in any future month = min(origDay=10, daysInMonth(targetM)).
        // For month 7: min(10, 30) = 10, so recurrence lands on 2082/07/10.
        svc.Add(new ReminderEntry
        {
            Title = "Monthly",
            BsDate = "2082/06/10",
            Recurrence = ReminderRecurrence.Monthly,
            Time = "09:00",
        });

        Assert.True(svc.HasRemindersForDateExpanded(2082, 7, 10));
        Assert.False(svc.HasRemindersForDateExpanded(2082, 7, 1));
    }

    // ── EndDate boundary ──────────────────────────────────────────────────────

    [Fact]
    public void GetRecurringForDate_WithEndDate_ExcludesAfterEndDate()
    {
        var svc = CreateService();
        svc.Add(new ReminderEntry
        {
            Title = "Bounded",
            BsDate = "2082/12/10",
            Recurrence = ReminderRecurrence.Daily,
            Time = "09:00",
            EndDate = "2082/12/15",
        });

        // Within range: should match
        var within = svc.GetRecurringForDate(2082, 12, 12);
        Assert.Single(within);

        // Day 15 is the end date. BsToAd(2082,12,15) = Apr 3 + (15-20) = Mar 29
        // BsToAd(2082,12,16) > BsToAd(2082,12,15) so past end
        var pastEnd = svc.GetRecurringForDate(2082, 12, 16);
        Assert.Empty(pastEnd);
    }

    [Fact]
    public void HasRemindersForDateExpanded_BeforeStartDate_ReturnsFalse()
    {
        var svc = CreateService();
        svc.Add(new ReminderEntry
        {
            Title = "Daily",
            BsDate = "2082/12/15",
            Recurrence = ReminderRecurrence.Daily,
            Time = "09:00",
        });

        Assert.False(svc.HasRemindersForDateExpanded(2082, 12, 10));
    }

    // ── CheckAndFire with EndDate expired ─────────────────────────────────────

    [Fact]
    public void CheckAndFire_EndDatePassed_RemovesRecurringReminder()
    {
        var svc = CreateService();
        // EndDate = 2082/12/19 => AD 2026-04-02 (via fake: 2026-04-03 + (19-20) = 2026-04-02)
        // For this to be "passed", DateTime.Now.Date must be > 2026-04-02
        svc.Add(new ReminderEntry
        {
            Title = "Expired Recurring",
            BsDate = "2082/12/20",
            Time = "00:01",
            Recurrence = ReminderRecurrence.Daily,
            EndDate = "2082/12/19", // AD: Apr 2, already passed relative to Apr 3
        });

        var fireUtc = new DateTime(2026, 4, 3, 0, 1, 0, DateTimeKind.Local).ToUniversalTime();
        svc.CheckAndFireDueReminders(fireUtc.AddMinutes(1));

        // The reminder with passed EndDate should have been removed
        Assert.Empty(svc.GetAll());
    }

    // ── Load migration: OriginalBsDate backfill ──────────────────────────────

    [Fact]
    public void Load_MissingOriginalBsDate_BackfillsFromBsDate()
    {
        var json = """
        [
          {
            "Id": "abc123",
            "Title": "No Original",
            "Notes": "",
            "BsDate": "2082/06/15",
            "OriginalBsDate": "",
            "Time": "10:00",
            "Recurrence": 0,
            "IsCompleted": false,
            "CreatedUtc": "2025-01-01T00:00:00Z"
          }
        ]
        """;
        File.WriteAllText(_filePath, json);

        var svc = CreateService();
        Assert.Equal("2082/06/15", svc.GetAll()[0].OriginalBsDate);
    }
}
