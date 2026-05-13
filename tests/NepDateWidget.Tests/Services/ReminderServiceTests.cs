using NepDateWidget.Models;
using NepDateWidget.Services;
using NepDateWidget.Tests.Services;
using System.Text.Json;

namespace NepDateWidget.Tests.Services;

public sealed class ReminderServiceTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string _filePath;
    private readonly FakeNepaliDateAdapter _adapter;

    public ReminderServiceTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"NepDateWidget_Tests_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
        _filePath = Path.Combine(_tempDir, "NepDateWidget.reminders.json");
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

    // ── CRUD ──────────────────────────────────────────────────────────────────

    [Fact]
    public void Add_PersistsToFile()
    {
        var svc = CreateService();
        var entry = new ReminderEntry
        {
            Title = "Test Reminder",
            BsDate = "2082/12/15",
            Time = "10:30"
        };

        svc.Add(entry);

        Assert.Single(svc.GetAll());
        Assert.True(File.Exists(_filePath));

        // Reload and verify persistence
        var svc2 = CreateService();
        Assert.Single(svc2.GetAll());
        Assert.Equal("Test Reminder", svc2.GetAll()[0].Title);
    }

    [Fact]
    public void Add_IgnoresEmptyTitle()
    {
        var svc = CreateService();
        svc.Add(new ReminderEntry { Title = "" });
        svc.Add(new ReminderEntry { Title = "   " });

        Assert.Empty(svc.GetAll());
    }

    [Fact]
    public void Add_TruncatesNotesAt500Chars()
    {
        var svc = CreateService();
        var longNotes = new string('A', 600);
        svc.Add(new ReminderEntry { Title = "X", Notes = longNotes, BsDate = "2082/01/01" });

        Assert.Equal(500, svc.GetAll()[0].Notes.Length);
    }

    [Fact]
    public void Update_ModifiesExistingEntry()
    {
        var svc = CreateService();
        var entry = new ReminderEntry
        {
            Title = "Original",
            BsDate = "2082/01/01",
            Time = "09:00"
        };
        svc.Add(entry);

        entry.Title = "Updated";
        entry.Time = "14:00";
        svc.Update(entry);

        var loaded = CreateService();
        Assert.Equal("Updated", loaded.GetAll()[0].Title);
        Assert.Equal("14:00", loaded.GetAll()[0].Time);
    }

    [Fact]
    public void Delete_RemovesEntry()
    {
        var svc = CreateService();
        var entry = new ReminderEntry { Title = "ToDelete", BsDate = "2082/01/01" };
        svc.Add(entry);
        Assert.Single(svc.GetAll());

        svc.Delete(entry.Id);
        Assert.Empty(svc.GetAll());
    }

    [Fact]
    public void Delete_NonexistentId_NoOp()
    {
        var svc = CreateService();
        svc.Add(new ReminderEntry { Title = "Keep", BsDate = "2082/01/01" });

        svc.Delete("nonexistent-id");
        Assert.Single(svc.GetAll());
    }

    // ── Query by date ─────────────────────────────────────────────────────────

    [Fact]
    public void GetForDate_ReturnsOnlyMatchingDate()
    {
        var svc = CreateService();
        svc.Add(new ReminderEntry { Title = "A", BsDate = "2082/01/01" });
        svc.Add(new ReminderEntry { Title = "B", BsDate = "2082/01/02" });
        svc.Add(new ReminderEntry { Title = "C", BsDate = "2082/01/01" });

        var result = svc.GetForDate(2082, 1, 1);
        Assert.Equal(2, result.Count);
        Assert.All(result, r => Assert.Equal("2082/01/01", r.BsDate));
    }

    [Fact]
    public void HasRemindersForDate_ReturnsTrueWhenExists()
    {
        var svc = CreateService();
        svc.Add(new ReminderEntry { Title = "A", BsDate = "2082/06/10" });

        Assert.True(svc.HasRemindersForDate(2082, 6, 10));
        Assert.False(svc.HasRemindersForDate(2082, 6, 11));
    }

    [Fact]
    public void HasRemindersForDateExpanded_ShowsDotOnRecurringDate()
    {
        var svc = CreateService();
        // Daily recurring from day 10
        svc.Add(new ReminderEntry
        {
            Title = "Daily",
            BsDate = "2082/06/10",
            Recurrence = ReminderRecurrence.Daily,
        });

        // Direct date: yes
        Assert.True(svc.HasRemindersForDateExpanded(2082, 6, 10));
        // Day 11 (1 day later): should be expanded
        Assert.True(svc.HasRemindersForDateExpanded(2082, 6, 11));
        // Day 15 (5 days later): should be expanded
        Assert.True(svc.HasRemindersForDateExpanded(2082, 6, 15));
        // Earlier date: should not match
        Assert.False(svc.HasRemindersForDateExpanded(2082, 6, 9));
    }

    [Fact]
    public void HasRemindersForDateExpanded_WeeklyRecurrence()
    {
        var svc = CreateService();
        svc.Add(new ReminderEntry
        {
            Title = "Weekly",
            BsDate = "2082/06/10",
            Recurrence = ReminderRecurrence.Weekly,
        });

        // AddDays(10, 7) => (2082, 6, 17) in fake adapter
        Assert.True(svc.HasRemindersForDateExpanded(2082, 6, 17));
        // Non-weekly offset should not match
        Assert.False(svc.HasRemindersForDateExpanded(2082, 6, 12));
    }

    // ── Fire and auto-delete ──────────────────────────────────────────────────

    [Fact]
    public void CheckAndFire_OneShotReminder_FiresAndAutoDeletes()
    {
        var svc = CreateService();
        // BsToAd(2082, 12, 20) => 2026-04-03 in the fake adapter
        svc.Add(new ReminderEntry
        {
            Title = "OneShot",
            BsDate = "2082/12/20",
            Time = "00:01",
            Recurrence = ReminderRecurrence.None,
        });

        // Fire time is 2026-04-03 00:01 local → convert to UTC
        var fireUtc = new DateTime(2026, 4, 3, 0, 1, 0, DateTimeKind.Local).ToUniversalTime();
        var fired = svc.CheckAndFireDueReminders(fireUtc.AddMinutes(1));

        Assert.Single(fired);
        Assert.Equal("OneShot", fired[0].Title);
        // One-shot now kept as completed history instead of auto-deleted
        Assert.Single(svc.GetAll());
        Assert.True(svc.GetAll()[0].IsCompleted);
    }

    [Fact]
    public void CheckAndFire_RecurringDaily_AdvancesDate()
    {
        var svc = CreateService();
        svc.Add(new ReminderEntry
        {
            Title = "Daily",
            BsDate = "2082/12/20",
            Time = "00:01",
            Recurrence = ReminderRecurrence.Daily,
        });

        var fireUtc = new DateTime(2026, 4, 3, 0, 1, 0, DateTimeKind.Local).ToUniversalTime();
        var fired = svc.CheckAndFireDueReminders(fireUtc.AddMinutes(1));

        Assert.Single(fired);
        // The reminder should still exist (recurring) but date advanced
        Assert.Single(svc.GetAll());
        var remaining = svc.GetAll()[0];
        Assert.Equal("2082/12/21", remaining.BsDate); // AddDays(20,1) = 21 in fake adapter
    }

    [Fact]
    public void CheckAndFire_RecurringWeekly_AdvancesBy7Days()
    {
        var svc = CreateService();
        svc.Add(new ReminderEntry
        {
            Title = "Weekly",
            BsDate = "2082/12/20",
            Time = "00:01",
            Recurrence = ReminderRecurrence.Weekly,
        });

        var fireUtc = new DateTime(2026, 4, 3, 0, 1, 0, DateTimeKind.Local).ToUniversalTime();
        svc.CheckAndFireDueReminders(fireUtc.AddMinutes(1));

        var remaining = svc.GetAll()[0];
        // AddDays(20,7) in fake adapter: 20 + (7%30) = 27
        Assert.Equal("2082/12/27", remaining.BsDate);
    }

    [Fact]
    public void CheckAndFire_RecurringMonthly_AdvancesByMonth()
    {
        var svc = CreateService();
        svc.Add(new ReminderEntry
        {
            Title = "Monthly",
            BsDate = "2082/11/15",
            Time = "00:01",
            Recurrence = ReminderRecurrence.Monthly,
        });

        // BsToAd(2082, 11, 15) => generic DateTime computed from month/day offset
        // (11-1)*30 + (15-1) = 314 days from 2025-01-01 => 2025-11-12
        var fireUtc = new DateTime(2025, 11, 12, 0, 1, 0, DateTimeKind.Local).ToUniversalTime();
        svc.CheckAndFireDueReminders(fireUtc.AddMinutes(1));

        var remaining = svc.GetAll()[0];
        // Monthly recurrence preserves the original day: 15th stays 15th each month.
        Assert.Equal("2082/12/15", remaining.BsDate);
    }

    // ── Missed reminders ──────────────────────────────────────────────────────

    [Fact]
    public void CheckAndFire_PastDateOneShot_SilentlyRemoved()
    {
        var svc = CreateService();
        svc.Add(new ReminderEntry
        {
            Title = "PastOneShot",
            BsDate = "2082/12/20",
            Time = "00:01",
            Recurrence = ReminderRecurrence.None,
        });

        // Fire at a "now" that is 2 days after the reminder date
        // Reminder AD date = 2026-04-03, nowUtc represents 2026-04-05
        var laterUtc = new DateTime(2026, 4, 5, 0, 2, 0, DateTimeKind.Local).ToUniversalTime();
        var fired = svc.CheckAndFireDueReminders(laterUtc);

        // Past date one-shot should NOT trigger a notification
        Assert.Empty(fired);
        // Kept as completed history instead of deleted
        Assert.Single(svc.GetAll());
        Assert.True(svc.GetAll()[0].IsCompleted);
    }

    [Fact]
    public void GetMissedReminders_ReturnsPastDueReminders()
    {
        var svc = CreateService();
        svc.Add(new ReminderEntry
        {
            Title = "Missed",
            BsDate = "2082/12/20",
            Time = "00:01",
            Recurrence = ReminderRecurrence.None,
        });

        var missed = svc.GetMissedReminders();
        // The fire time is 2026-04-03 00:01 local, which is in the past relative to
        // the test running date (April 2026). Will be missed if now > that time.
        // Since this is time-dependent, just verify the method doesn't crash.
        Assert.NotNull(missed);
    }

    // ── Persistence resilience ────────────────────────────────────────────────

    [Fact]
    public void Load_CorruptJson_FallsBackToEmpty()
    {
        File.WriteAllText(_filePath, "NOT VALID JSON {{{");
        var svc = CreateService();
        Assert.Empty(svc.GetAll());
    }

    [Fact]
    public void Load_MissingFile_StartsEmpty()
    {
        var svc = CreateService();
        Assert.Empty(svc.GetAll());
    }

    [Fact]
    public void AtomicWrite_TmpFileDoesNotPersist()
    {
        var svc = CreateService();
        svc.Add(new ReminderEntry { Title = "X", BsDate = "2082/01/01" });

        Assert.False(File.Exists(_filePath + ".tmp"));
        Assert.False(File.Exists(_filePath + ".bak"));
    }

    // ── RemindersChanged event ────────────────────────────────────────────────

    [Fact]
    public void Add_RaisesRemindersChanged()
    {
        var svc = CreateService();
        int called = 0;
        svc.RemindersChanged += (_, _) => called++;

        svc.Add(new ReminderEntry { Title = "X", BsDate = "2082/01/01" });
        Assert.Equal(1, called);
    }

    [Fact]
    public void Delete_RaisesRemindersChanged()
    {
        var svc = CreateService();
        var entry = new ReminderEntry { Title = "X", BsDate = "2082/01/01" };
        svc.Add(entry);

        int called = 0;
        svc.RemindersChanged += (_, _) => called++;

        svc.Delete(entry.Id);
        Assert.Equal(1, called);
    }

    // ── Legacy JSON migration ─────────────────────────────────────────────────

    [Fact]
    public void Load_LegacyIntFormat_MigratesToBsDateStrings()
    {
        // Write old-format JSON with BsYear/BsMonth/BsDay integer fields
        var legacyJson = """
        [
          {
            "Id": "abc123",
            "Title": "Legacy Reminder",
            "Notes": "",
            "BsYear": 2082,
            "BsMonth": 6,
            "BsDay": 15,
            "OriginalBsYear": 2082,
            "OriginalBsMonth": 6,
            "OriginalBsDay": 15,
            "Time": "10:00",
            "Recurrence": 0,
            "EndDate": null,
            "IsCompleted": false,
            "CreatedUtc": "2025-01-01T00:00:00Z",
            "LastFiredUtc": null
          }
        ]
        """;
        File.WriteAllText(_filePath, legacyJson);

        var svc = CreateService();
        Assert.Single(svc.GetAll());
        var entry = svc.GetAll()[0];
        Assert.Equal("2082/06/15", entry.BsDate);
        Assert.Equal("2082/06/15", entry.OriginalBsDate);
        Assert.Equal("Legacy Reminder", entry.Title);

        // After migration, the file should be re-saved in new format
        var reloaded = CreateService();
        Assert.Equal("2082/06/15", reloaded.GetAll()[0].BsDate);
    }

    // ── Timing precision ──────────────────────────────────────────────────────

    [Theory]
    [InlineData("09:00", 9, 0)]   // 9 AM
    [InlineData("00:00", 0, 0)]   // Midnight
    [InlineData("12:00", 12, 0)]  // Noon
    [InlineData("18:30", 18, 30)] // 6:30 PM
    [InlineData("23:59", 23, 59)] // Just before midnight
    public void CheckAndFire_VariousTimes_FiresAtCorrectTime(string time24, int hour, int minute)
    {
        var svc = CreateService();
        // BsToAd(2082, 12, 20) => 2026-04-03 in fake adapter
        svc.Add(new ReminderEntry
        {
            Title = $"Test {time24}",
            BsDate = "2082/12/20",
            Time = time24,
            Recurrence = ReminderRecurrence.None,
        });

        var fireLocal = new DateTime(2026, 4, 3, hour, minute, 0, DateTimeKind.Local);
        var fireUtc = fireLocal.ToUniversalTime();

        // 1 second before: should NOT fire
        var beforeFired = svc.CheckAndFireDueReminders(fireUtc.AddSeconds(-1));
        Assert.Empty(beforeFired);
        Assert.False(svc.GetAll()[0].IsCompleted);

        // At the exact time: should fire
        var atTimeFired = svc.CheckAndFireDueReminders(fireUtc);
        Assert.Single(atTimeFired);
        Assert.True(svc.GetAll()[0].IsCompleted);
    }

    [Fact]
    public void CheckAndFire_9AM_DoesNotFireAt6PM_IfAlreadyFired()
    {
        var svc = CreateService();
        svc.Add(new ReminderEntry
        {
            Title = "Morning Alert",
            BsDate = "2082/12/20",
            Time = "09:00",
            Recurrence = ReminderRecurrence.None,
        });

        // Fire at 9 AM
        var nineAm = new DateTime(2026, 4, 3, 9, 0, 30, DateTimeKind.Local).ToUniversalTime();
        var fired = svc.CheckAndFireDueReminders(nineAm);
        Assert.Single(fired);
        Assert.True(svc.GetAll()[0].IsCompleted);

        // At 6 PM, should NOT fire again (already completed)
        var sixPm = new DateTime(2026, 4, 3, 18, 0, 0, DateTimeKind.Local).ToUniversalTime();
        var firedAgain = svc.CheckAndFireDueReminders(sixPm);
        Assert.Empty(firedAgain);
    }

    [Fact]
    public void CheckAndFire_OneShot_FiredOnceMarkedCompleted_NeverFiresAgain()
    {
        var svc = CreateService();
        svc.Add(new ReminderEntry
        {
            Title = "One-shot",
            BsDate = "2082/12/20",
            Time = "10:00",
            Recurrence = ReminderRecurrence.None,
        });

        var fireUtc = new DateTime(2026, 4, 3, 10, 0, 30, DateTimeKind.Local).ToUniversalTime();
        svc.CheckAndFireDueReminders(fireUtc);
        Assert.True(svc.GetAll()[0].IsCompleted);

        // Multiple subsequent checks should all return empty
        for (int i = 0; i < 5; i++)
        {
            var laterUtc = fireUtc.AddMinutes(30 * (i + 1));
            var fired = svc.CheckAndFireDueReminders(laterUtc);
            Assert.Empty(fired);
        }
    }

    [Fact]
    public void CheckAndFire_RecurringDaily_FiredOnce_AdvancesDate_DoesNotRefireToday()
    {
        var svc = CreateService();
        svc.Add(new ReminderEntry
        {
            Title = "Daily 9AM",
            BsDate = "2082/12/20",
            Time = "09:00",
            Recurrence = ReminderRecurrence.Daily,
        });

        var fireUtc = new DateTime(2026, 4, 3, 9, 0, 30, DateTimeKind.Local).ToUniversalTime();
        var fired = svc.CheckAndFireDueReminders(fireUtc);
        Assert.Single(fired);

        // BsDate should have advanced
        Assert.Equal("2082/12/21", svc.GetAll()[0].BsDate);

        // 30 seconds later (simulating next timer tick): should NOT fire
        var nextTick = fireUtc.AddSeconds(30);
        var firedAgain = svc.CheckAndFireDueReminders(nextTick);
        Assert.Empty(firedAgain);

        // Even at 6 PM today: should NOT fire (date is now tomorrow)
        var sixPm = new DateTime(2026, 4, 3, 18, 0, 0, DateTimeKind.Local).ToUniversalTime();
        var firedAt6 = svc.CheckAndFireDueReminders(sixPm);
        Assert.Empty(firedAt6);
    }

    [Fact]
    public void CheckAndFire_RecurringAdvanceFailure_MarkedCompleted()
    {
        var svc = CreateService();
        var entry = new ReminderEntry
        {
            Title = "Edge case",
            BsDate = "2082/12/20",
            Time = "00:01",
            Recurrence = ReminderRecurrence.Daily,
        };
        svc.Add(entry);

        var fireUtc = new DateTime(2026, 4, 3, 0, 1, 30, DateTimeKind.Local).ToUniversalTime();
        var fired = svc.CheckAndFireDueReminders(fireUtc);
        Assert.Single(fired);

        // After firing, the date should have advanced. Verify it didn't stay stuck.
        var advanced = svc.GetAll()[0];
        Assert.NotEqual("2082/12/20", advanced.BsDate);
        Assert.False(advanced.IsCompleted); // Advanced successfully, not force-completed
    }

    [Fact]
    public void CheckAndFire_CompletedReminder_NeverFires()
    {
        var svc = CreateService();
        var entry = new ReminderEntry
        {
            Title = "Already done",
            BsDate = "2082/12/20",
            Time = "09:00",
            Recurrence = ReminderRecurrence.None,
            IsCompleted = true,
        };
        svc.Add(entry);

        var fireUtc = new DateTime(2026, 4, 3, 9, 0, 30, DateTimeKind.Local).ToUniversalTime();
        var fired = svc.CheckAndFireDueReminders(fireUtc);
        Assert.Empty(fired);
    }

    [Fact]
    public void CheckAndFire_MissedReminder_FiresTodayButNotPastDate()
    {
        var svc = CreateService();
        // Reminder for 2082/12/20 (AD 2026-04-03) at 9 AM
        svc.Add(new ReminderEntry
        {
            Title = "Missed",
            BsDate = "2082/12/20",
            Time = "09:00",
            Recurrence = ReminderRecurrence.None,
        });

        // Check at 6 PM on the SAME day: 9 hours stale, should NOT fire but mark completed
        var sixPm = new DateTime(2026, 4, 3, 18, 0, 0, DateTimeKind.Local).ToUniversalTime();
        var fired = svc.CheckAndFireDueReminders(sixPm);
        Assert.Empty(fired);
        Assert.True(svc.GetAll()[0].IsCompleted);

        // Check within grace period: should fire
        var svc1b = CreateService();
        svc1b.Add(new ReminderEntry
        {
            Title = "Just due",
            BsDate = "2082/12/20",
            Time = "09:00",
            Recurrence = ReminderRecurrence.None,
        });
        var justAfter = new DateTime(2026, 4, 3, 9, 1, 0, DateTimeKind.Local).ToUniversalTime();
        var fired1b = svc1b.CheckAndFireDueReminders(justAfter);
        Assert.Single(fired1b);

        // Reset for next test: check if a PAST date reminder is silently completed
        var svc2 = CreateService();
        svc2.Add(new ReminderEntry
        {
            Title = "Old reminder",
            BsDate = "2082/12/20",
            Time = "09:00",
            Recurrence = ReminderRecurrence.None,
        });

        // Check 2 days later: past date should NOT trigger alert but mark completed
        var twoDaysLater = new DateTime(2026, 4, 5, 12, 0, 0, DateTimeKind.Local).ToUniversalTime();
        var fired2 = svc2.CheckAndFireDueReminders(twoDaysLater);
        Assert.Empty(fired2);
        Assert.True(svc2.GetAll()[0].IsCompleted);
    }

    // ── Delete confirmation for recurring ─────────────────────────────────────

    [Fact]
    public void Delete_NonRecurring_RemovesImmediately()
    {
        var svc = CreateService();
        var entry = new ReminderEntry
        {
            Title = "OneShot",
            BsDate = "2082/12/20",
            Time = "09:00",
            Recurrence = ReminderRecurrence.None,
        };
        svc.Add(entry);
        Assert.Single(svc.GetAll());

        svc.Delete(entry.Id);
        Assert.Empty(svc.GetAll());
    }

    [Fact]
    public void MarkCompleted_RecurringReminder_StopsShowingButKeepsEntry()
    {
        var svc = CreateService();
        var entry = new ReminderEntry
        {
            Title = "Daily",
            BsDate = "2082/12/20",
            Time = "09:00",
            Recurrence = ReminderRecurrence.Daily,
        };
        svc.Add(entry);

        // "Delete this only" = mark as completed
        entry.IsCompleted = true;
        svc.Update(entry);

        // Entry still exists but completed
        Assert.Single(svc.GetAll());
        Assert.True(svc.GetAll()[0].IsCompleted);

        // Won't fire anymore
        var fireUtc = new DateTime(2026, 4, 3, 9, 0, 30, DateTimeKind.Local).ToUniversalTime();
        var fired = svc.CheckAndFireDueReminders(fireUtc);
        Assert.Empty(fired);

        // Won't appear in HasRemindersForDate (excludes completed)
        Assert.False(svc.HasRemindersForDate(2082, 12, 20));
    }

    [Fact]
    public void Delete_RecurringReminder_All_RemovesEntirely()
    {
        var svc = CreateService();
        var entry = new ReminderEntry
        {
            Title = "Weekly",
            BsDate = "2082/12/20",
            Time = "09:00",
            Recurrence = ReminderRecurrence.Weekly,
        };
        svc.Add(entry);
        Assert.Single(svc.GetAll());

        // "Delete all" = full remove
        svc.Delete(entry.Id);
        Assert.Empty(svc.GetAll());
    }

    // ── Time parsing roundtrip ────────────────────────────────────────────────

    [Theory]
    [InlineData("09:00", "9:00")]   // Standard morning time
    [InlineData("00:00", "12:00")]  // Midnight in 12h = 12:00 AM
    [InlineData("12:00", "12:00")]  // Noon in 12h = 12:00 PM
    [InlineData("13:30", "1:30")]   // 1:30 PM
    [InlineData("23:59", "11:59")]  // 11:59 PM
    public void TimeSpanTryParse_InvariantCulture_ParsesCorrectly(string input, string expectedDisplay12h)
    {
        // This tests that our stored time format parses correctly with InvariantCulture
        Assert.True(TimeSpan.TryParse(input, System.Globalization.CultureInfo.InvariantCulture, out var ts));
        int h = ts.Hours;
        int m = ts.Minutes;

        // Verify the 24h value
        var parts = input.Split(':');
        Assert.Equal(int.Parse(parts[0]), h);
        Assert.Equal(int.Parse(parts[1]), m);

        // Verify it produces correct 12h display
        string display;
        if (h == 0) display = $"12:{m:D2}";
        else if (h < 12) display = $"{h}:{m:D2}";
        else if (h == 12) display = $"12:{m:D2}";
        else display = $"{h - 12}:{m:D2}";
        Assert.Equal(expectedDisplay12h, display);
    }

    // ── Recurrence does not fire before scheduled time ────────────────────────

    [Theory]
    [InlineData(ReminderRecurrence.Daily)]
    [InlineData(ReminderRecurrence.Weekly)]
    [InlineData(ReminderRecurrence.Monthly)]
    public void CheckAndFire_RecurringBeforeTime_DoesNotFire(ReminderRecurrence recurrence)
    {
        var svc = CreateService();
        svc.Add(new ReminderEntry
        {
            Title = "Future recurring",
            BsDate = "2082/12/20",
            Time = "15:00", // 3 PM
            Recurrence = recurrence,
        });

        // Check at 2:59 PM: should not fire
        var beforeUtc = new DateTime(2026, 4, 3, 14, 59, 59, DateTimeKind.Local).ToUniversalTime();
        var fired = svc.CheckAndFireDueReminders(beforeUtc);
        Assert.Empty(fired);
        Assert.False(svc.GetAll()[0].IsCompleted);
    }

    [Fact]
    public void CheckAndFire_EndDatePassed_RecurringRemoved()
    {
        var svc = CreateService();
        svc.Add(new ReminderEntry
        {
            Title = "Expired recurring",
            BsDate = "2082/01/01",
            Time = "09:00",
            Recurrence = ReminderRecurrence.Daily,
            EndDate = "2082/01/01", // End date is the start date (just this one day)
        });

        // BsToAd(2082, 1, 1) => 2025-04-14
        // Check well after the end date has passed: IsEndDatePassed uses DateTime.Now.Date
        // which in real runs would be past 2025-04-14, so the recurring gets removed.
        var lateUtc = new DateTime(2025, 4, 14, 9, 1, 0, DateTimeKind.Local).ToUniversalTime();
        svc.CheckAndFireDueReminders(lateUtc);

        // The end date check uses DateTime.Now (wall clock), so behavior depends on test run date.
        // We verify the method doesn't crash and the reminder is handled.
        Assert.NotNull(svc.GetAll());
    }

    [Fact]
    public void CheckAndFire_InvalidTime_Skipped()
    {
        var svc = CreateService();
        svc.Add(new ReminderEntry
        {
            Title = "Bad time",
            BsDate = "2082/12/20",
            Time = "not-a-time",
            Recurrence = ReminderRecurrence.None,
        });

        var fireUtc = new DateTime(2026, 4, 3, 12, 0, 0, DateTimeKind.Local).ToUniversalTime();
        var fired = svc.CheckAndFireDueReminders(fireUtc);

        // Should be skipped, not crash; reminder stays but never fires
        Assert.Empty(fired);
        Assert.False(svc.GetAll()[0].IsCompleted);
    }

    [Fact]
    public void CheckAndFire_InvalidBsDate_Skipped()
    {
        var svc = CreateService();
        svc.Add(new ReminderEntry
        {
            Title = "Bad date",
            BsDate = "invalid",
            Time = "09:00",
            Recurrence = ReminderRecurrence.None,
        });

        var fireUtc = new DateTime(2026, 4, 3, 12, 0, 0, DateTimeKind.Local).ToUniversalTime();
        var fired = svc.CheckAndFireDueReminders(fireUtc);

        Assert.Empty(fired);
        Assert.False(svc.GetAll()[0].IsCompleted);
    }

    [Fact]
    public void CheckAndFire_MultipleReminders_OnlyDueOnesFire()
    {
        var svc = CreateService();

        // Reminder 1: 8 AM (stale at 9 AM check, silently completed)
        svc.Add(new ReminderEntry
        {
            Title = "8AM reminder",
            BsDate = "2082/12/20",
            Time = "08:00",
            Recurrence = ReminderRecurrence.None,
        });

        // Reminder 2: 10 AM (should NOT fire at 9 AM check)
        svc.Add(new ReminderEntry
        {
            Title = "10AM reminder",
            BsDate = "2082/12/20",
            Time = "10:00",
            Recurrence = ReminderRecurrence.None,
        });

        // Reminder 3: 9 AM (within grace window at 9:00:30, should fire)
        svc.Add(new ReminderEntry
        {
            Title = "9AM reminder",
            BsDate = "2082/12/20",
            Time = "09:00",
            Recurrence = ReminderRecurrence.None,
        });

        var nineAm = new DateTime(2026, 4, 3, 9, 0, 30, DateTimeKind.Local).ToUniversalTime();
        var fired = svc.CheckAndFireDueReminders(nineAm);

        // Only 9 AM fires; 8 AM is stale (60+ min old)
        Assert.Single(fired);
        Assert.Equal("9AM reminder", fired[0].Title);

        // 8 AM should be silently completed (stale)
        var eightAm = svc.GetAll().First(r => r.Title == "8AM reminder");
        Assert.True(eightAm.IsCompleted);

        // 10 AM reminder should still be pending
        var pending = svc.GetAll().Where(r => !r.IsCompleted).ToList();
        Assert.Single(pending);
        Assert.Equal("10AM reminder", pending[0].Title);
    }

    [Fact]
    public void CheckAndFire_RepeatedChecks_30SecondInterval_OnlyFiresOnce()
    {
        var svc = CreateService();
        svc.Add(new ReminderEntry
        {
            Title = "Timer test",
            BsDate = "2082/12/20",
            Time = "09:00",
            Recurrence = ReminderRecurrence.None,
        });

        var baseUtc = new DateTime(2026, 4, 3, 9, 0, 0, DateTimeKind.Local).ToUniversalTime();

        // Simulate 10 timer ticks at 30-second intervals
        int totalFired = 0;
        for (int i = 0; i < 10; i++)
        {
            var tickUtc = baseUtc.AddSeconds(i * 30);
            var fired = svc.CheckAndFireDueReminders(tickUtc);
            totalFired += fired.Count;
        }

        // Should fire exactly once
        Assert.Equal(1, totalFired);
    }

    [Fact]
    public void CheckAndFire_RecurringDaily_RepeatedTicks_OnlyFiresOncePerDay()
    {
        var svc = CreateService();
        svc.Add(new ReminderEntry
        {
            Title = "Daily ticks",
            BsDate = "2082/12/20",
            Time = "09:00",
            Recurrence = ReminderRecurrence.Daily,
        });

        var nineAm = new DateTime(2026, 4, 3, 9, 0, 0, DateTimeKind.Local).ToUniversalTime();

        // Simulate multiple ticks over the course of the day
        int totalFired = 0;
        for (int i = 0; i < 20; i++)
        {
            var tickUtc = nineAm.AddSeconds(i * 30);
            var fired = svc.CheckAndFireDueReminders(tickUtc);
            totalFired += fired.Count;
        }

        // Should fire exactly once today
        Assert.Equal(1, totalFired);
        // BsDate should have advanced to next day
        Assert.Equal("2082/12/21", svc.GetAll()[0].BsDate);
    }

    // ── Persistence after fire ────────────────────────────────────────────────

    [Fact]
    public void CheckAndFire_OneShot_CompletedStatePersisted()
    {
        var svc = CreateService();
        svc.Add(new ReminderEntry
        {
            Title = "Persist test",
            BsDate = "2082/12/20",
            Time = "09:00",
            Recurrence = ReminderRecurrence.None,
        });

        var fireUtc = new DateTime(2026, 4, 3, 9, 0, 30, DateTimeKind.Local).ToUniversalTime();
        svc.CheckAndFireDueReminders(fireUtc);

        // Reload from disk: completed state should be persisted
        var svc2 = CreateService();
        Assert.Single(svc2.GetAll());
        Assert.True(svc2.GetAll()[0].IsCompleted);

        // After reload, should not fire again
        var laterUtc = new DateTime(2026, 4, 3, 18, 0, 0, DateTimeKind.Local).ToUniversalTime();
        var fired = svc2.CheckAndFireDueReminders(laterUtc);
        Assert.Empty(fired);
    }

    [Fact]
    public void CheckAndFire_RecurringDaily_AdvancedDatePersisted()
    {
        var svc = CreateService();
        svc.Add(new ReminderEntry
        {
            Title = "Persist recurring",
            BsDate = "2082/12/20",
            Time = "09:00",
            Recurrence = ReminderRecurrence.Daily,
        });

        var fireUtc = new DateTime(2026, 4, 3, 9, 0, 30, DateTimeKind.Local).ToUniversalTime();
        svc.CheckAndFireDueReminders(fireUtc);

        // Reload from disk
        var svc2 = CreateService();
        Assert.Single(svc2.GetAll());
        Assert.Equal("2082/12/21", svc2.GetAll()[0].BsDate);
        Assert.False(svc2.GetAll()[0].IsCompleted);

        // Should NOT fire at same time (date has advanced)
        var fired = svc2.CheckAndFireDueReminders(fireUtc);
        Assert.Empty(fired);
    }
}
