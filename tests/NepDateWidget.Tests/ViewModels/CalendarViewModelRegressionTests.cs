using NepDateWidget.Models;
using NepDateWidget.Services;
using NepDateWidget.Tests.Services;
using NepDateWidget.ViewModels;

namespace NepDateWidget.Tests.ViewModels;

/// <summary>
/// Regression tests for CalendarViewModel that specifically target:
///
/// 1. Stale HasReminders/HasNote on cells that transition from current-month
///    to padding during in-place navigation (RefreshGrid fast + slow path).
///
/// 2. Stale VisibleEvents / HasVisibleEvents when navigating away from a month
///    whose cells had events at positions that become padding in the new month.
///
/// 3. NavigationRequested event-subscription leak: CalendarViewModel is a
///    singleton; the View (CalendarView) subscribes on DataContext assignment.
///    When ExpandedShellWindow is destroyed and recreated, the old subscription
///    was NOT removed because DataContextChanged with a null OldValue was fired
///    instead of the old VM. Each expand added one more subscriber, causing
///    navigation to jump N months instead of one. Unloaded now fixes this.
///    These tests document the observable symptoms so the regression cannot recur.
/// </summary>
public class CalendarViewModelRegressionTests
{
    // ════════════════════════════════════════════════════════════════════════
    // Infrastructure
    // ════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Wraps FakeNepaliDateAdapter (sealed) so individual BS dates can have
    /// custom events / tithi. Used to seed a month with visible events before
    /// navigating away to verify they are cleared.
    /// </summary>
    private sealed class EventfulAdapter : INepaliDateAdapter
    {
        private readonly FakeNepaliDateAdapter _inner = new();
        private readonly Dictionary<(int Y, int M, int D), (string[] EventsEn, string TithiEn)> _cfg = new();

        public void Set(int y, int m, int d, string[] eventsEn, string tithiEn = "")
            => _cfg[(y, m, d)] = (eventsEn, tithiEn);

        // Delegate all standard calls to _inner:
        public (int Year, int Month, int Day) GetTodayBs()    => _inner.GetTodayBs();
        public DateTime GetTodayAd()                          => _inner.GetTodayAd();
        public int GetDaysInMonth(int y, int m)               => _inner.GetDaysInMonth(y, m);
        public DayOfWeek GetFirstDayOfMonth(int y, int m)     => _inner.GetFirstDayOfMonth(y, m);
        public DateTime? BsToAd(int y, int m, int d)          => _inner.BsToAd(y, m, d);
        public (int, int, int)? AdToBs(DateTime dt)           => _inner.AdToBs(dt);
        public string GetMonthNameEn(int y, int m)            => _inner.GetMonthNameEn(y, m);
        public string GetMonthNameNe(int m)                   => _inner.GetMonthNameNe(m);
        public string FormatBsShortEn(int y, int m, int d)    => _inner.FormatBsShortEn(y, m, d);
        public string FormatBsShortNe(int y, int m, int d)    => _inner.FormatBsShortNe(y, m, d);
        public string FormatBsLongEn(int y, int m, int d)     => _inner.FormatBsLongEn(y, m, d);
        public string FormatBsLongNe(int y, int m, int d)     => _inner.FormatBsLongNe(y, m, d);
        public DayOfWeek GetDayOfWeek(int y, int m, int d)    => _inner.GetDayOfWeek(y, m, d);
        public (int, int, int)? AddDays(int y, int m, int d, int days) => _inner.AddDays(y, m, d, days);
        public (int, int, int)? AddMonths(int y, int m, int d, int months) => _inner.AddMonths(y, m, d, months);
        public int? DiffTotalDays(int y1, int m1, int d1, int y2, int m2, int d2)
            => _inner.DiffTotalDays(y1, m1, d1, y2, m2, d2);
        public (int Years, int Months, int Days)? DiffBreakdown(int y1, int m1, int d1, int y2, int m2, int d2)
            => _inner.DiffBreakdown(y1, m1, d1, y2, m2, d2);
        public (string FyLabel, int Quarter, int DaysToQuarterEnd, int DaysToYearEnd) GetFiscalYearInfo(int y, int m, int d)
            => _inner.GetFiscalYearInfo(y, m, d);
        public bool TryParseSmartBsDate(string raw, out int y, out int m, out int d)
            => _inner.TryParseSmartBsDate(raw, out y, out m, out d);
        public (bool IsPublicHoliday, string TithiEn, string TithiNp, string[] EventsEn, string[] EventsNp)
            GetCalendarInfo(int y, int m, int d) => _inner.GetCalendarInfo(y, m, d);

        public (bool IsPublicHoliday, string TithiEn, string TithiNp, string[] EventsEn, string[] EventsNp,
                DateTime? AdDate, string BsShortEn, string BsShortNe, string BsLongEn, string BsLongNe)
            GetCellData(int y, int m, int d)
        {
            var r = _inner.GetCellData(y, m, d);
            if (!_cfg.TryGetValue((y, m, d), out var c))
            {
                return r;
            }

            return (r.IsPublicHoliday, c.TithiEn, c.TithiEn.Length > 0 ? c.TithiEn + "-np" : "",
                    c.EventsEn, c.EventsEn.Select(e => e + "-np").ToArray(),
                    r.AdDate, r.BsShortEn, r.BsShortNe, r.BsLongEn, r.BsLongNe);
        }
    }

    /// <summary>Reminder service where specific months/days can be armed with reminders.</summary>
    private sealed class ConfigurableReminderService : IReminderService
    {
        private readonly Dictionary<(int Y, int M), HashSet<int>> _map = new();
        public event EventHandler? RemindersChanged;

        public void SetReminders(int bsYear, int bsMonth, params int[] days)
            => _map[(bsYear, bsMonth)] = new HashSet<int>(days);

        public void FireChanged() => RemindersChanged?.Invoke(this, EventArgs.Empty);

        public HashSet<int> GetHasRemindersForMonth(int bsYear, int bsMonth)
            => _map.TryGetValue((bsYear, bsMonth), out var s) ? s : new HashSet<int>();

        public bool HasRemindersForDate(int y, int m, int d) => GetHasRemindersForMonth(y, m).Contains(d);
        public bool HasRemindersForDateExpanded(int y, int m, int d) => HasRemindersForDate(y, m, d);
        public IReadOnlyList<ReminderEntry> GetAll()                         => Array.Empty<ReminderEntry>();
        public IReadOnlyList<ReminderEntry> GetForDate(int y, int m, int d)  => Array.Empty<ReminderEntry>();
        public IReadOnlyList<ReminderEntry> GetRecurringForDate(int y, int m, int d) => Array.Empty<ReminderEntry>();
        public void Add(ReminderEntry e)    { }
        public void Update(ReminderEntry e) { }
        public void Delete(string id)       { }
        public void Load()                  { }
        public void Save()                  { }
        public IReadOnlyList<ReminderEntry> CheckAndFireDueReminders(DateTime now) => Array.Empty<ReminderEntry>();
        public IReadOnlyList<ReminderEntry> GetMissedReminders()                   => Array.Empty<ReminderEntry>();
    }

    /// <summary>Notes service where specific date keys can have a note pre-seeded.</summary>
    private sealed class ConfigurableNotesService : INotesService
    {
        private readonly Dictionary<string, string> _notes = new();
        public event EventHandler? NotesChanged;

        public void Seed(string dateKey, string text) => _notes[dateKey] = text;
        public string? GetNote(string dateKey)         => _notes.GetValueOrDefault(dateKey);
        public IReadOnlyDictionary<string, string> GetAll() => _notes;
        public HashSet<int> GetHasNotesForMonth(int bsYear, int bsMonth)
        {
            var result = new HashSet<int>();
            string prefix = $"{bsYear:D4}-{bsMonth:D2}-";
            foreach (var key in _notes.Keys)
            {
                if (key.Length == prefix.Length + 2
                    && key.StartsWith(prefix, StringComparison.Ordinal)
                    && int.TryParse(key.AsSpan(prefix.Length), out int day))
                {
                    result.Add(day);
                }
            }
            return result;
        }
        public void SetNote(string dateKey, string? text)
        {
            if (string.IsNullOrEmpty(text))
            {
                _notes.Remove(dateKey);
            }
            else
            {
                _notes[dateKey] = text!;
            }

            NotesChanged?.Invoke(this, EventArgs.Empty);
        }
        public void DeleteNote(string dateKey)
        {
            _notes.Remove(dateKey);
            NotesChanged?.Invoke(this, EventArgs.Empty);
        }
        public void Load() { }
        public void Save() { }
    }

    // ── Helper: create CalendarViewModel starting at BS 2082/12 ──────────────
    //
    // Layout of BS 2082/12 (Chaitra) with FakeNepaliDateAdapter:
    //   starts Tuesday (DayOfWeek=2) → leadingPad=2, 30 days, totalCells=35
    //   cell[0..1]  = leading padding
    //   cell[2..31] = days 1..30 of 2082/12
    //   cell[32..34]= trailing padding
    //
    // Layout of BS 2082/11 (Falgun):
    //   starts Saturday (DayOfWeek=6) → leadingPad=6, 30 days, totalCells=42
    //   cell[0..5]  = leading padding
    //   cell[6..35] = days 1..30 of 2082/11
    //   cell[36..41]= trailing padding
    //
    // Navigating 2082/12 → 2082/11 triggers the SLOW PATH (35→42 cells):
    //   overlap=35 cells updated in-place, 7 new cells appended.
    //   cell[2] goes from day-1-of-2082/12 (current-month) to padding.
    //   This is the critical position for stale-dot and stale-events regressions.
    //
    // Navigating 2082/11 → 2082/12 also triggers the SLOW PATH (42→35 cells):
    //   overlap=35 updated in-place, 7 trailing cells removed.
    //   cell[2] goes from padding to day-1-of-2082/12.

    private static CalendarViewModel CreateAt12(
        INepaliDateAdapter? adapter = null,
        ConfigurableReminderService? rs = null,
        ConfigurableNotesService? ns = null)
    {
        var a    = adapter ?? new FakeNepaliDateAdapter();
        var svc  = new CalendarService(a);
        var loc  = new LocalizationService(TestPaths.DefaultLocalizationPath);
        var conv = new ConversionService(a);
        return new CalendarViewModel(svc, loc, conv,
            adapter: a,
            reminderService: rs,
            notesService: ns);
        // CalendarViewModel defaults to today which FakeNepaliDateAdapter pins at 2082/12/20
    }

    // ════════════════════════════════════════════════════════════════════════
    // REGRESSION: stale HasReminders after navigation (slow path)
    // Bug: RefreshGrid only reset HasReminders when day.IsCurrentMonth was true.
    // Padding cells whose backing day has IsCurrentMonth=false never got reset.
    // Fix: Always write: Days[i].HasReminders = day.IsCurrentMonth && reminderDays.Contains(day.Day)
    // ════════════════════════════════════════════════════════════════════════

    [Fact]
    public void RefreshGrid_SlowPath_PaddingCellAfterCurrentMonth_ClearsHasReminders()
    {
        // Arm day 1 of 2082/12 with a reminder. Cell[2] will have HasReminders=true.
        var rs = new ConfigurableReminderService();
        rs.SetReminders(2082, 12, 1);

        var vm = CreateAt12(rs: rs);
        Assert.True(vm.Days[2].HasReminders, "Precondition: cell[2] is day-1 of 2082/12 and has a reminder.");

        // Navigate back to 2082/11. Cell[2] becomes a padding cell (leading 6 pads).
        vm.PrevMonthCommand.Execute(null);

        Assert.True(vm.Days[2].IsPadding, "Cell[2] must be padding in 2082/11 (leading 6 pads).");
        Assert.False(vm.Days[2].HasReminders, "HasReminders must be false for a padding cell - stale reminder dot regression.");
    }

    [Fact]
    public void RefreshGrid_SlowPath_PaddingCellAfterCurrentMonth_ClearsHasRemindersDots_AllPositions()
    {
        // Arm the first 6 days so that cells[2..7] are armed in 2082/12.
        // After navigating to 2082/11, cells[0..5] are padding; those that
        // were previously current-month (cells[2..5]) must be false.
        var rs = new ConfigurableReminderService();
        rs.SetReminders(2082, 12, 1, 2, 3, 4, 5, 6);

        var vm = CreateAt12(rs: rs);
        // Cells 2-7 are days 1-6 of 2082/12 → all armed
        for (int i = 2; i <= 7; i++)
        {
            Assert.True(vm.Days[i].HasReminders, $"Precondition: cell[{i}] should be armed.");
        }

        vm.PrevMonthCommand.Execute(null); // → 2082/11

        // Cells 0-5 are now padding in 2082/11 - none may show a reminder dot.
        for (int i = 0; i <= 5; i++)
        {
            Assert.True(vm.Days[i].IsPadding, $"Cell[{i}] must be padding in 2082/11.");
            Assert.False(vm.Days[i].HasReminders, $"Cell[{i}] (padding) must not show a reminder dot.");
        }
    }

    // ════════════════════════════════════════════════════════════════════════
    // REGRESSION: stale HasNote after navigation
    // Same bug / same fix as HasReminders above.
    // ════════════════════════════════════════════════════════════════════════

    [Fact]
    public void RefreshGrid_SlowPath_PaddingCellAfterCurrentMonth_ClearsHasNote()
    {
        // Seed a note for 2082/12/01. NotesService.FormatKey → "2082-12-01".
        var ns = new ConfigurableNotesService();
        ns.Seed(NotesService.FormatKey(2082, 12, 1), "Birthday reminder");

        var vm = CreateAt12(ns: ns);
        Assert.True(vm.Days[2].HasNote, "Precondition: cell[2] is day-1 of 2082/12 and has a note.");

        vm.PrevMonthCommand.Execute(null); // → 2082/11

        Assert.True(vm.Days[2].IsPadding, "Cell[2] must be padding in 2082/11.");
        Assert.False(vm.Days[2].HasNote, "HasNote must be false for a padding cell - stale note-dot regression.");
    }

    // ════════════════════════════════════════════════════════════════════════
    // REGRESSION: stale VisibleEvents after navigation
    // Bug: UpdateVisibleEventCount silently reset _visibleEventsArray to empty but
    // did NOT fire PropertyChanged(VisibleEvents). WPF binding stayed stale.
    // Fix: Added proper clear-with-notification path in UpdateVisibleEventCount.
    // ════════════════════════════════════════════════════════════════════════

    [Fact]
    public void RefreshGrid_SlowPath_PaddingCellAfterCurrentMonth_ClearsVisibleEvents()
    {
        // Day 1 of 2082/12 has an event. After navigation to 2082/11, cell[2]
        // is padding and must show no events.
        var adapter = new EventfulAdapter();
        adapter.Set(2082, 12, 1, eventsEn: ["Vijaya Dashami"]);

        var vm = CreateAt12(adapter: adapter);
        Assert.True(vm.Days[2].HasVisibleEvents, "Precondition: day-1 of 2082/12 has a visible event.");
        Assert.Single(vm.Days[2].VisibleEvents);

        vm.PrevMonthCommand.Execute(null); // → 2082/11

        Assert.True(vm.Days[2].IsPadding);
        Assert.Empty(vm.Days[2].VisibleEvents);
        Assert.False(vm.Days[2].HasVisibleEvents);
    }

    [Fact]
    public void RefreshGrid_SlowPath_PaddingCellAfterCurrentMonth_ClearsHasHiddenEvents()
    {
        // Day 1 of 2082/12 has 2 events. With default count=1 → HasHiddenEvents=true.
        // After navigating to 2082/11, cell[2] becomes padding → HasHiddenEvents=false.
        var adapter = new EventfulAdapter();
        adapter.Set(2082, 12, 1, eventsEn: ["Event A", "Event B"]);

        var vm = CreateAt12(adapter: adapter);
        Assert.True(vm.Days[2].HasHiddenEvents, "Precondition: overflow events are hidden.");

        vm.PrevMonthCommand.Execute(null);

        Assert.False(vm.Days[2].HasHiddenEvents);
    }

    // ════════════════════════════════════════════════════════════════════════
    // REGRESSION: stale Tithi after navigation (slow path)
    // TithiText uses SetProperty so it fires on any value change. These tests
    // confirm the transition works end-to-end at the CalendarViewModel level.
    // ════════════════════════════════════════════════════════════════════════

    [Fact]
    public void RefreshGrid_SlowPath_PaddingCellAfterCurrentMonth_ClearsTithiText()
    {
        var adapter = new EventfulAdapter();
        adapter.Set(2082, 12, 1, eventsEn: Array.Empty<string>(), tithiEn: "Dashami");

        var vm = CreateAt12(adapter: adapter);
        Assert.Equal("Dashami", vm.Days[2].TithiText);
        Assert.True(vm.Days[2].ShowTithiText);

        vm.PrevMonthCommand.Execute(null); // → 2082/11

        Assert.Equal(string.Empty, vm.Days[2].TithiText);
        Assert.False(vm.Days[2].ShowTithiText);
    }

    // ════════════════════════════════════════════════════════════════════════
    // RefreshGrid FAST PATH: verify same correctness when cell count is unchanged
    // (navigate between two months with the same total cell count)
    // ════════════════════════════════════════════════════════════════════════

    [Fact]
    public void RefreshGrid_FastPath_PaddingCellAfterCurrentMonth_ClearsHasReminders()
    {
        // Find two adjacent months with the same totalCells count so RefreshGrid
        // uses the fast path (no ObservableCollection changes).
        // 2082/01 starts Saturday (leadingPad=6), 31 days → totalCells = ceil(37/7)*7 = 42
        // 2082/02 starts Monday (leadingPad=1), 32 days → totalCells = ceil(33/7)*7 = 35
        // Different → won't trigger fast path.
        //
        // 2082/04 starts Saturday (leadingPad=6), 32 days → totalCells = ceil(38/7)*7 = 42
        // 2082/05 starts Tuesday (leadingPad=2), 31 days → totalCells = ceil(33/7)*7 = 35
        // Different again.
        //
        // Let's find a same-count pair:
        // 2082/07 starts Saturday  (pad=6), 30d → ceil(36/7)*7=42
        // 2082/08 starts Monday    (pad=1), 29d → ceil(30/7)*7=35
        // Still different.
        //
        // 2082/06 starts Thursday  (pad=4), 30d → ceil(34/7)*7=35
        // 2082/07 starts Saturday  (pad=6), 30d → ceil(36/7)*7=42
        // Different.
        //
        // 2082/09 starts Wednesday (pad=3), 30d → ceil(33/7)*7=35
        // 2082/10 starts Thursday  (pad=4), 29d → ceil(33/7)*7=35 ← SAME!
        //
        // Navigate 2082/10 → 2082/09: fast path (both 35 cells).
        // 2082/10 starts Thursday (pad=4): cells[0..3]=pad, cells[4..32]=days1..29, cells[33..34]=trail-pad
        // 2082/09 starts Wednesday (pad=3): cells[0..2]=pad, cells[3..32]=days1..30, cells[33..34]=trail-pad
        // Cell[3] in 2082/10 = day 0 of 2082/10 = padding (index 3 < leadingPad 4)
        // Wait, cell[4] = day 1 of 2082/10. Cell[3] is padding in 2082/10.
        // Cell[3] in 2082/09 = day 1 of 2082/09 (index 3, leadingPad=3, dayNumber=3-3+1=1)
        // So cell[3] transitions FROM padding (2082/10) TO day-1-of-2082/09.
        // We want the REVERSE: from current-month WITH reminder TO padding.
        // cell[4] in 2082/10 = day 1 (has reminder).
        // cell[4] in 2082/09 = day 2 (no reminder) - still current-month. Not padding.
        // Hmm, with same leading padding difference, hard to find a position that goes
        // from current-month to padding.
        //
        // Let us instead use a navigateTO scenario:
        // 2082/09 has pad=3: cell[0,1,2]=padding, cell[3..32]=days 1..30.
        // 2082/10 has pad=4: cell[0,1,2,3]=padding, cell[4..32]=days 1..29.
        // Navigate 2082/09 → 2082/10: fast path (35==35).
        // cell[3]: 2082/09 → day 1 (current-month, can have reminder). 2082/10 → padding.
        // This is exactly the transition we need!
        var a = new FakeNepaliDateAdapter { TodayBsYear = 2082, TodayBsMonth = 9, TodayBsDay = 15 };
        var rs = new ConfigurableReminderService();
        rs.SetReminders(2082, 9, 1); // day 1 of 2082/09 has a reminder

        var svc  = new CalendarService(a);
        var loc  = new LocalizationService(TestPaths.DefaultLocalizationPath);
        var conv = new ConversionService(a);
        var vm   = new CalendarViewModel(svc, loc, conv, adapter: a, reminderService: rs);

        // 2082/09 starts Wednesday (pad=3): cell[3] = day 1
        int cellIdx = 3;
        Assert.False(vm.Days[cellIdx].IsPadding, $"Precondition: cell[{cellIdx}] must be current-month (day 1) in 2082/09.");
        Assert.True(vm.Days[cellIdx].HasReminders, $"Precondition: cell[{cellIdx}] must have reminder.");

        // Fast-path navigation to 2082/10 (same cell count=35)
        vm.NextMonthCommand.Execute(null);

        Assert.Equal(35, vm.Days.Count);   // confirms fast path (no count change)
        Assert.True(vm.Days[cellIdx].IsPadding, $"Cell[{cellIdx}] must be padding in 2082/10 (pad=4).");
        Assert.False(vm.Days[cellIdx].HasReminders, "HasReminders must be cleared for padding cell in fast path.");
    }

    [Fact]
    public void RefreshGrid_FastPath_PaddingCellAfterCurrentMonth_ClearsHasNote()
    {
        // Same fast-path scenario as above: 2082/09 → 2082/10, cell[3].
        var a = new FakeNepaliDateAdapter { TodayBsYear = 2082, TodayBsMonth = 9, TodayBsDay = 15 };
        var ns = new ConfigurableNotesService();
        ns.Seed(NotesService.FormatKey(2082, 9, 1), "Appointment");

        var svc  = new CalendarService(a);
        var loc  = new LocalizationService(TestPaths.DefaultLocalizationPath);
        var conv = new ConversionService(a);
        var vm   = new CalendarViewModel(svc, loc, conv, adapter: a, notesService: ns);

        Assert.True(vm.Days[3].HasNote, "Precondition: cell[3] has a note in 2082/09.");
        vm.NextMonthCommand.Execute(null); // → 2082/10, fast path
        Assert.Equal(35, vm.Days.Count);
        Assert.True(vm.Days[3].IsPadding);
        Assert.False(vm.Days[3].HasNote, "HasNote must be cleared for padding cell in fast path.");
    }

    [Fact]
    public void RefreshGrid_FastPath_PaddingCellAfterCurrentMonth_ClearsVisibleEvents()
    {
        // Navigate 2082/12 → 2082/3 using NavigateTo: same total cell count (35), so fast path.
        //   2082/12 starts Tuesday  (leadingPad=2), 30d → 35 cells. cell[2] = day 1.
        //   2082/3  starts Thursday (leadingPad=4), 31d → 35 cells. cell[2] = padding.
        // Day 1 of 2082/12 is seeded with events. After NavigateTo(2082,3) cell[2]
        // must show no events (fast-path in-place update clears VisibleEvents).
        var adapter = new EventfulAdapter();
        adapter.Set(2082, 12, 1, eventsEn: ["Festival"]);
        var vm = CreateAt12(adapter: adapter);

        Assert.True(vm.Days[2].HasVisibleEvents, "Precondition: day-1 of 2082/12 has events.");

        vm.NavigateTo(2082, 3);

        Assert.Equal(35, vm.Days.Count); // count unchanged confirms fast path
        Assert.True(vm.Days[2].IsPadding, "Cell[2] must be padding in 2082/3 (leadingPad=4).");
        Assert.False(vm.Days[2].HasVisibleEvents, "HasVisibleEvents must be false for padding cell - fast-path events-repeating regression.");
        Assert.Empty(vm.Days[2].VisibleEvents);
    }

    // ════════════════════════════════════════════════════════════════════════
    // REGRESSION: NavigationRequested subscription leak
    //
    // Symptom: after N expand/collapse cycles, clicking Next/Prev jumps N months.
    // Root cause: CalendarView subscribes to NavigationRequested on DataContext
    // assignment. When ExpandedShellWindow is destroyed, DataContextChanged fired
    // with OldValue=null (no VM to unsubscribe from). The next expand subscribed
    // again, leaving two handlers on the event. N cycles → N handlers → N jumps.
    // Fix: CalendarView.Unloaded now unsubscribes unconditionally.
    //
    // These tests verify observable symptoms at the VM event level so the
    // regression cannot silently return.
    // ════════════════════════════════════════════════════════════════════════

    [Fact]
    public void NavigateMonths_WithNoExternalSubscribers_AdvancesExactlyOneMonth()
    {
        // Baseline: no view subscribed. NavigateMonths must advance exactly 1 month.
        var vm = CreateAt12();
        Assert.Equal(2082, vm.DisplayYear);
        Assert.Equal(12, vm.DisplayMonth);

        vm.NavigateMonths(1);

        Assert.Equal(2083, vm.DisplayYear);
        Assert.Equal(1, vm.DisplayMonth);
    }

    [Fact]
    public void NavigateMonths_WithSingleSubscriber_AdvancesExactlyOneMonth()
    {
        // A single view subscription (correct state): must still advance 1 month.
        var vm = CreateAt12();
        vm.NavigationRequested += (_, doNav) => doNav();

        vm.NavigateMonths(1);

        Assert.Equal(2083, vm.DisplayYear);
        Assert.Equal(1, vm.DisplayMonth);
    }

    [Fact]
    public void NavigateMonths_WithDoubleSubscription_JumpsTwoMonths_DocumentsLeakBehavior()
    {
        // Two subscribers (simulates pre-fix: old CalendarView still subscribed +
        // new CalendarView added on second expand). Each calls doNav() → two advances.
        // This test DOCUMENTS the symptom so the regression is detectable by name.
        var vm = CreateAt12();
        vm.NavigationRequested += (_, doNav) => doNav();
        vm.NavigationRequested += (_, doNav) => doNav(); // stale second subscription

        vm.NavigateMonths(1);

        // Two doNav() calls: 2082/12 → 2083/01 → 2083/02
        Assert.Equal(2083, vm.DisplayYear);
        Assert.Equal(2, vm.DisplayMonth); // jumped 2 months, not 1
    }

    [Fact]
    public void NavigateMonths_AfterUnsubscribeOne_AdvancesExactlyOneMonth()
    {
        // Simulates Unloaded removing one of two stale subscriptions (the fix).
        var vm = CreateAt12();
        Action<int, Action> handler = (_, doNav) => doNav();
        vm.NavigationRequested += handler;
        vm.NavigationRequested += handler;   // second subscription (bug scenario)
        vm.NavigationRequested -= handler;   // Unloaded removes one (the fix)

        vm.NavigateMonths(1);

        Assert.Equal(2083, vm.DisplayYear);
        Assert.Equal(1, vm.DisplayMonth);    // exactly one month: fix works
    }

    [Fact]
    public void NavigateMonths_BackwardWithDoubleSubscription_JumpsTwoMonthsBack()
    {
        // Backward navigation has the same leak multiplier.
        var vm = CreateAt12();
        vm.NavigationRequested += (_, doNav) => doNav();
        vm.NavigationRequested += (_, doNav) => doNav();

        vm.NavigateMonths(-1);

        // 2082/12 → 2082/11 → 2082/10
        Assert.Equal(2082, vm.DisplayYear);
        Assert.Equal(10, vm.DisplayMonth);
    }

    // ════════════════════════════════════════════════════════════════════════
    // General grid correctness after navigation
    // ════════════════════════════════════════════════════════════════════════

    [Fact]
    public void RefreshGrid_AfterNavigation_DayCountIsMultipleOf7()
    {
        var vm = CreateAt12();
        for (int i = 0; i < 12; i++)
        {
            vm.NavigateMonths(1);
            Assert.True(vm.Days.Count % 7 == 0, $"Month {i + 1} after start: Days.Count ({vm.Days.Count}) must be a multiple of 7.");
        }
    }

    [Fact]
    public void RefreshGrid_AfterNavigation_PaddingCellsHaveEmptyDayText()
    {
        var vm = CreateAt12();
        vm.NavigateMonths(1); // move to a different month
        var paddingCells = vm.Days.Where(d => d.IsPadding).ToList();
        Assert.All(paddingCells, d => Assert.Equal(string.Empty, d.DayText));
    }

    [Fact]
    public void RefreshGrid_AfterNavigation_CurrentMonthCellsHaveNonEmptyDayText()
    {
        var vm = CreateAt12();
        vm.NavigateMonths(1);
        var currentCells = vm.Days.Where(d => d.IsCurrentMonth).ToList();
        Assert.All(currentCells, d => Assert.NotEmpty(d.DayText));
    }

    [Fact]
    public void RefreshGrid_RoundTrip_DayCountUnchanged_WhenSameMonthRevisited()
    {
        var vm = CreateAt12();
        int original = vm.Days.Count;
        vm.NavigateMonths(1);
        vm.NavigateMonths(-1); // back to 2082/12
        Assert.Equal(original, vm.Days.Count);
    }

    [Fact]
    public void RefreshGrid_SlowPath_NewCellsForLargerMonth_HaveNoStaleReminders()
    {
        // Navigate from smaller month (2082/12, 35 cells) to larger month (2082/11, 42 cells).
        // The 7 newly-appended cells are fresh (constructed, not updated). They must
        // not carry HasReminders from any previous state.
        var rs = new ConfigurableReminderService();
        rs.SetReminders(2082, 12, 1, 2, 3, 4, 5, 6); // arm many days in current month

        var vm = CreateAt12(rs: rs);
        vm.PrevMonthCommand.Execute(null); // → 2082/11, slow path adds 7 cells

        // The new cells were appended for days 29..30 of 2082/11 (approx positions 35-41).
        // They have no reminder configured for 2082/11 so must be false.
        var newCells = vm.Days.Skip(35).Take(7).ToList();
        Assert.All(newCells, d => Assert.False(d.HasReminders, "Newly-appended cells must not have stale HasReminders."));
    }

    [Fact]
    public void RefreshGrid_NavigateTo_DoesNotAdvanceMonthMoreThanRequested()
    {
        // NavigateTo is used for "Go Today" and direct month combo picks.
        // Verify it always lands exactly on the requested month regardless of
        // how many NavigationRequested subscribers are attached.
        var vm = CreateAt12();
        vm.NavigationRequested += (_, doNav) => doNav(); // single clean subscription

        vm.NavigateTo(2082, 6);

        Assert.Equal(2082, vm.DisplayYear);
        Assert.Equal(6, vm.DisplayMonth);
    }

    // ════════════════════════════════════════════════════════════════════════
    // GRID PRECISION: exact cell positions for 2082/12
    //
    // 2082/12 starts Tuesday (DayOfWeek=2) → leadingPad=2, 30 days.
    // Layout: cells[0..1]=leading-pad, cells[2..31]=days1..30, cells[32..34]=trailing-pad.
    // Today: FakeNepaliDateAdapter pins today at 2082/12/20 → cell[21] (pad + day-1 offset).
    // ════════════════════════════════════════════════════════════════════════

    [Fact]
    public void TodayCell_IsAtExpectedCellIndex()
    {
        // Day 20 → cell index = leadingPad(2) + day(20) - 1 = 21.
        var vm = CreateAt12();
        Assert.True(vm.Days[21].IsToday, "Today (day 20) must be at cell[21].");
        Assert.Equal(20, vm.Days[21].Day);
    }

    [Fact]
    public void TodayCell_ExactlyOne_InMonth()
    {
        var vm = CreateAt12();
        int count = vm.Days.Count(d => d.IsToday);
        Assert.Equal(1, count);
    }

    [Fact]
    public void LeadingPaddingCells_ExactlyTwo_In2082_12()
    {
        // leadingPad=2 for 2082/12 (starts Tuesday).
        var vm = CreateAt12();
        int leadingPad = vm.Days.TakeWhile(d => d.IsPadding).Count();
        Assert.Equal(2, leadingPad);
    }

    [Fact]
    public void CurrentMonthCells_ExactlyThirty_In2082_12()
    {
        var vm = CreateAt12();
        int count = vm.Days.Count(d => d.IsCurrentMonth);
        Assert.Equal(30, count);
    }

    [Fact]
    public void CurrentMonthCells_HaveSequentialDayNumbers()
    {
        // cells[2..31] must have Day=1..30 in order.
        var vm = CreateAt12();
        var currentCells = vm.Days.Where(d => d.IsCurrentMonth).ToList();
        for (int i = 0; i < currentCells.Count; i++)
        {
            Assert.Equal(i + 1, currentCells[i].Day);
        }
    }

    [Fact]
    public void TotalCells_Is35_In2082_12()
    {
        var vm = CreateAt12();
        Assert.Equal(35, vm.Days.Count);
    }

    // ════════════════════════════════════════════════════════════════════════
    // WIDGET EXPAND / COLLAPSE LIFECYCLE
    //
    // The ExpandedShellWindow is created fresh on each expand and destroyed on
    // each collapse. CalendarViewModel is a singleton (owned by MainViewModel).
    //
    // Each expand creates a new CalendarView which subscribes to NavigationRequested.
    // Each collapse fires CalendarView.Unloaded which unsubscribes.
    // After N correct expand/collapse cycles, exactly 1 subscription remains.
    //
    // Without the Unloaded fix, each expand added one more subscription. N
    // cycles → N subscriptions → navigation jumped N months per click.
    //
    // The helpers below simulate the View's subscription lifecycle:
    //   SimulateExpand  = CalendarView loaded  → DataContextChanged → subscribe
    //   SimulateCollapse = CalendarView.Unloaded → unsubscribe
    // ════════════════════════════════════════════════════════════════════════

    private static (Action Expand, Action Collapse) ViewLifecycle(CalendarViewModel vm)
    {
        Action<int, Action>? handler = null;
        return (
            Expand: () =>
            {
                handler = (_, doNav) => doNav();
                vm.NavigationRequested += handler;
            },
            Collapse: () =>
            {
                if (handler is not null)
                {
                    vm.NavigationRequested -= handler;
                }

                handler = null;
            }
        );
    }

    [Fact]
    public void ExpandCollapse_SingleCycle_WithCleanup_NavigatesExactlyOneMonth()
    {
        var vm = CreateAt12();
        var (expand, collapse) = ViewLifecycle(vm);

        expand();
        collapse(); // widget collapsed
        expand();   // widget expanded again - should have exactly 1 subscription

        vm.NavigateMonths(1);

        Assert.Equal(2083, vm.DisplayYear);
        Assert.Equal(1, vm.DisplayMonth);
    }

    [Fact]
    public void ExpandCollapse_FiveCycles_WithCleanup_NavigatesExactlyOneMonth()
    {
        // Simulate a user who opens and closes the widget 5 times.
        // After the 5th open, navigation must still advance exactly 1 month.
        var vm = CreateAt12();

        for (int i = 0; i < 5; i++)
        {
            var (expand, collapse) = ViewLifecycle(vm);
            expand();
            collapse();
        }

        // 6th open: final state has exactly 1 subscriber
        var (exp, _) = ViewLifecycle(vm);
        exp();

        vm.NavigateMonths(1);

        Assert.Equal(2083, vm.DisplayYear);
        Assert.Equal(1, vm.DisplayMonth);
    }

    [Fact]
    public void ExpandCollapse_ThreeCycles_WithoutCleanup_JumpsThreeMonths_DocumentsPreFixBug()
    {
        // Simulates the pre-fix behavior: Unloaded handler was missing.
        // Each expand added a subscription; collapse never removed it.
        // After 3 expands without cleanup → 3 subscriptions → 3-month jump.
        var vm = CreateAt12();

        // Subscribe 3 times (no matching unsubscribes - the old bug)
        vm.NavigationRequested += (_, doNav) => doNav();
        vm.NavigationRequested += (_, doNav) => doNav();
        vm.NavigationRequested += (_, doNav) => doNav();

        vm.NavigateMonths(1);

        // Each handler calls doNav(): 3 advances from 2082/12.
        Assert.Equal(2083, vm.DisplayYear);
        Assert.Equal(3, vm.DisplayMonth); // jumped 3 months, not 1
    }

    [Fact]
    public void ExpandCollapse_StatePreserved_DisplayMonthUnchangedAfterCollapse()
    {
        // CalendarViewModel is a singleton. Navigation state must survive
        // a collapse/expand cycle - the user left off at a specific month.
        var vm = CreateAt12();
        var (expand, collapse) = ViewLifecycle(vm);

        expand();
        vm.NavigateTo(2082, 6); // user navigated to Ashwin before closing
        collapse(); // widget collapsed - VM stays alive

        // Widget reopened - should show the same month the user was on
        Assert.Equal(2082, vm.DisplayYear);
        Assert.Equal(6, vm.DisplayMonth);
    }

    [Fact]
    public void ExpandCollapse_AfterReopen_GridReflectsPreservedMonth()
    {
        // Not only should DisplayMonth be correct, the cell grid must reflect
        // the preserved month (cells should be from 2082/06, not 2082/12).
        var vm = CreateAt12();
        var (expand, collapse) = ViewLifecycle(vm);

        expand();
        vm.NavigateTo(2082, 6);
        collapse();
        expand(); // reopen

        // 2082/06 starts Thursday (leadingPad=4, 30 days → 35 cells).
        Assert.Equal(35, vm.Days.Count);
        Assert.Equal(4, vm.Days.TakeWhile(d => d.IsPadding).Count());
        Assert.Equal(30, vm.Days.Count(d => d.IsCurrentMonth));
    }

    [Fact]
    public void ExpandCollapse_GoTodayAfterReopen_LandsOnTodaysMonth()
    {
        var vm = CreateAt12();
        var (expand, collapse) = ViewLifecycle(vm);

        expand();
        vm.NavigateTo(2082, 3); // navigate somewhere else
        collapse();
        expand(); // reopen
        vm.GoTodayCommand.Execute(null);

        Assert.Equal(2082, vm.DisplayYear);
        Assert.Equal(12, vm.DisplayMonth); // back to today's month
        Assert.True(vm.IsShowingToday);
    }

    [Fact]
    public void ExpandCollapse_EventsFromPreviousMonth_DoNotShowAfterCollapseAndNavigate()
    {
        // User was on a month with events, collapsed, reopened on a different month.
        // No stale events from the old month should appear.
        var adapter = new EventfulAdapter();
        adapter.Set(2082, 12, 1, eventsEn: ["Old Event"]);
        var vm = CreateAt12(adapter: adapter);

        var (expand, collapse) = ViewLifecycle(vm);
        expand();
        Assert.True(vm.Days[2].HasVisibleEvents, "Precondition: day-1 of 2082/12 has events.");

        // User collapses the widget
        collapse();

        // VM navigates internally (e.g. user typed a date in the pill bar while collapsed)
        vm.NavigateTo(2082, 11); // 2082/11: cell[2] is padding (leadingPad=6)

        expand(); // reopen

        // cell[2] is padding in 2082/11 - no events from the old month should bleed through
        Assert.True(vm.Days[2].IsPadding);
        Assert.False(vm.Days[2].HasVisibleEvents);
        Assert.Empty(vm.Days[2].VisibleEvents);
    }

    [Fact]
    public void ExpandCollapse_ReminderDotFromPreviousMonth_DoesNotShowAfterNavigate()
    {
        // Same pattern as above but for reminder dots.
        var rs = new ConfigurableReminderService();
        rs.SetReminders(2082, 12, 1);
        var vm = CreateAt12(rs: rs);

        var (expand, collapse) = ViewLifecycle(vm);
        expand();
        Assert.True(vm.Days[2].HasReminders, "Precondition: day-1 of 2082/12 has a reminder.");

        collapse();
        vm.NavigateTo(2082, 11); // cell[2] becomes padding
        expand();

        Assert.True(vm.Days[2].IsPadding);
        Assert.False(vm.Days[2].HasReminders);
    }

    // ════════════════════════════════════════════════════════════════════════
    // LIVE DATA UPDATES: RemindersChanged without navigation
    //
    // When the user opens the day-detail popup and adds a reminder, the
    // reminder service fires RemindersChanged. CalendarViewModel responds by
    // calling RefreshReminderDots(), which updates dots on current-month cells
    // without triggering a full navigation or grid rebuild.
    // ════════════════════════════════════════════════════════════════════════

    [Fact]
    public void RemindersChanged_LiveUpdate_ShowsDotOnDay()
    {
        // Day 4 is at cell[2+4-1]=cell[5] in 2082/12.
        var rs = new ConfigurableReminderService();
        var vm = CreateAt12(rs: rs);

        Assert.False(vm.Days[5].HasReminders, "Precondition: no reminder.");

        rs.SetReminders(2082, 12, 4); // arm day 4
        rs.FireChanged();             // simulate: user saved a reminder, service fired event

        Assert.True(vm.Days[5].HasReminders, "Dot must appear after RemindersChanged fires.");
    }

    [Fact]
    public void RemindersChanged_LiveUpdate_HidesDotWhenReminderRemoved()
    {
        var rs = new ConfigurableReminderService();
        rs.SetReminders(2082, 12, 4);
        var vm = CreateAt12(rs: rs);

        Assert.True(vm.Days[5].HasReminders, "Precondition: reminder present.");

        rs.SetReminders(2082, 12); // clear all reminders for month (empty params)
        rs.FireChanged();

        Assert.False(vm.Days[5].HasReminders, "Dot must disappear when reminder removed.");
    }

    [Fact]
    public void RemindersChanged_LiveUpdate_DoesNotChangeDisplayMonth()
    {
        var rs = new ConfigurableReminderService();
        var vm = CreateAt12(rs: rs);

        vm.NavigateTo(2082, 6); // navigate to a different month first
        Assert.Equal(6, vm.DisplayMonth);

        rs.SetReminders(2082, 6, 10);
        rs.FireChanged(); // triggers RefreshReminderDots for the CURRENT display month

        // Month must stay at 6 - RemindersChanged must NOT trigger navigation
        Assert.Equal(2082, vm.DisplayYear);
        Assert.Equal(6, vm.DisplayMonth);
    }

    [Fact]
    public void RemindersChanged_LiveUpdate_UpdatesCurrentlyDisplayedMonth_Not_OriginalMonth()
    {
        // User navigates away from 2082/12 to 2082/06.
        // A reminder fires for 2082/06. The dots in 2082/06 must update,
        // and the dots in 2082/12 (no longer visible) are irrelevant.
        // 2082/06 starts Thursday (leadingPad=4). day 10 → cell[4+10-1]=cell[13].
        var rs = new ConfigurableReminderService();
        var vm = CreateAt12(rs: rs);
        vm.NavigateTo(2082, 6);

        rs.SetReminders(2082, 6, 10);
        rs.FireChanged();

        Assert.True(vm.Days[13].HasReminders, "Dot must appear on day 10 of the currently displayed month (2082/06).");
        Assert.Equal(10, vm.Days[13].Day);
    }

    [Fact]
    public void RemindersChanged_LiveUpdate_PaddingCellsNeverReceiveDot()
    {
        // Arm every possible day number. RefreshReminderDots must skip padding cells.
        var rs = new ConfigurableReminderService();
        rs.SetReminders(2082, 12, Enumerable.Range(1, 30).ToArray());

        var vm = CreateAt12(rs: rs);
        rs.FireChanged(); // trigger live refresh

        var paddingCells = vm.Days.Where(d => d.IsPadding).ToList();
        Assert.NotEmpty(paddingCells);
        Assert.All(paddingCells, d => Assert.False(d.HasReminders, "Padding cells must never show a reminder dot."));
    }

    [Fact]
    public void RemindersChanged_LiveUpdate_MultipleDotsInSameMonth()
    {
        var rs = new ConfigurableReminderService();
        var vm = CreateAt12(rs: rs);

        rs.SetReminders(2082, 12, 1, 10, 20, 30);
        rs.FireChanged();

        // day 1  → cell[2],  day 10 → cell[11], day 20 → cell[21], day 30 → cell[31]
        Assert.True(vm.Days[2].HasReminders,  "day 1");
        Assert.True(vm.Days[11].HasReminders, "day 10");
        Assert.True(vm.Days[21].HasReminders, "day 20 (= today cell)");
        Assert.True(vm.Days[31].HasReminders, "day 30");
    }

    [Fact]
    public void RemindersChanged_LiveUpdate_ClearsAllDotsWhenMonthHasNoReminders()
    {
        var rs = new ConfigurableReminderService();
        rs.SetReminders(2082, 12, 1, 5, 10, 15);
        var vm = CreateAt12(rs: rs);

        Assert.True(vm.Days[2].HasReminders, "Precondition: day 1 armed.");

        rs.SetReminders(2082, 12); // clear all
        rs.FireChanged();

        var currentCells = vm.Days.Where(d => d.IsCurrentMonth).ToList();
        Assert.All(currentCells, d => Assert.False(d.HasReminders, "All reminder dots must clear."));
    }

    // ════════════════════════════════════════════════════════════════════════
    // LIVE DATA UPDATES: NotesChanged without navigation
    // ════════════════════════════════════════════════════════════════════════

    [Fact]
    public void NotesChanged_LiveUpdate_ShowsDotOnDay()
    {
        // Day 7 → cell[2+7-1]=cell[8] in 2082/12.
        var ns = new ConfigurableNotesService();
        var vm = CreateAt12(ns: ns);

        Assert.False(vm.Days[8].HasNote, "Precondition: no note.");

        // SetNote fires NotesChanged → VM calls RefreshNoteDots
        ns.SetNote(NotesService.FormatKey(2082, 12, 7), "Meeting notes");

        Assert.True(vm.Days[8].HasNote, "Note dot must appear after NotesChanged fires.");
    }

    [Fact]
    public void NotesChanged_LiveUpdate_HidesDotWhenNoteDeleted()
    {
        var ns = new ConfigurableNotesService();
        ns.Seed(NotesService.FormatKey(2082, 12, 7), "Draft");
        var vm = CreateAt12(ns: ns);

        Assert.True(vm.Days[8].HasNote, "Precondition: note present.");

        ns.DeleteNote(NotesService.FormatKey(2082, 12, 7)); // fires NotesChanged

        Assert.False(vm.Days[8].HasNote, "Note dot must disappear after note deleted.");
    }

    [Fact]
    public void NotesChanged_LiveUpdate_PaddingCellsNeverReceiveDot()
    {
        // Add notes for day 0 (invalid key) and also fire the event.
        // Padding cells must remain false.
        var ns = new ConfigurableNotesService();
        var vm = CreateAt12(ns: ns);

        // Seed notes for every real day in the month
        for (int d = 1; d <= 30; d++)
        {
            ns.Seed(NotesService.FormatKey(2082, 12, d), "note");
        }

        // Force a NotesChanged event by deleting one real note (triggers RefreshNoteDots)
        ns.DeleteNote(NotesService.FormatKey(2082, 12, 1));

        var paddingCells = vm.Days.Where(d => d.IsPadding).ToList();
        Assert.NotEmpty(paddingCells);
        Assert.All(paddingCells, d => Assert.False(d.HasNote, "Padding cells must not have a note dot."));
    }

    [Fact]
    public void NotesChanged_LiveUpdate_DoesNotChangeDisplayMonth()
    {
        var ns = new ConfigurableNotesService();
        var vm = CreateAt12(ns: ns);
        vm.NavigateTo(2082, 6);

        ns.SetNote(NotesService.FormatKey(2082, 6, 1), "note");

        Assert.Equal(2082, vm.DisplayYear);
        Assert.Equal(6, vm.DisplayMonth); // no navigation side-effect
    }

    // ════════════════════════════════════════════════════════════════════════
    // CELL LAYOUT: UpdateCellLayout
    //
    // CalendarView calls UpdateCellLayout when the grid resizes. This adjusts
    // font sizes and the visible-event count. The visible count directly controls
    // how many event rows show per cell and whether the "..." overflow indicator
    // appears. On reopen, the View will call this again once it renders - so the
    // grid must respond correctly to these calls at any time.
    // ════════════════════════════════════════════════════════════════════════

    [Fact]
    public void UpdateCellLayout_LargerCell_IncreasesVisibleEvents()
    {
        // Day 1 of 2082/12 has 3 events. Default visibleCount=1 → only 1 shows.
        // After UpdateCellLayout with large cell → all 3 must be visible.
        var adapter = new EventfulAdapter();
        adapter.Set(2082, 12, 1, eventsEn: ["Event A", "Event B", "Event C"]);
        var vm = CreateAt12(adapter: adapter);

        // Default: visibleCount=1 → 1 visible, 2 hidden
        Assert.Single(vm.Days[2].VisibleEvents);
        Assert.True(vm.Days[2].HasHiddenEvents);

        // Simulate the View reporting large cell dimensions (e.g. 200px × 114px)
        // cellHeight=200, cellWidth=114 → ws=1.0, eventsH=150.8, visibleCount=8
        vm.UpdateCellLayout(cellHeight: 200, cellWidth: 114);

        Assert.Equal(3, vm.Days[2].VisibleEvents.Count);
        Assert.False(vm.Days[2].HasHiddenEvents);
    }

    [Fact]
    public void UpdateCellLayout_SmallerCell_ClampedToMinimumOne()
    {
        // Tiny cell dimensions: ws clamps to 0.65, eventsH goes negative → max(1, ...) = 1.
        var adapter = new EventfulAdapter();
        adapter.Set(2082, 12, 1, eventsEn: ["Event A", "Event B"]);
        var vm = CreateAt12(adapter: adapter);

        vm.UpdateCellLayout(cellHeight: 10, cellWidth: 20);

        Assert.Single(vm.Days[2].VisibleEvents);
    }

    [Fact]
    public void UpdateCellLayout_ZeroDimensions_IsNoOp()
    {
        // Zero-dimension call (e.g. before layout pass) must not crash or change state.
        var adapter = new EventfulAdapter();
        adapter.Set(2082, 12, 1, eventsEn: ["Event A", "Event B"]);
        var vm = CreateAt12(adapter: adapter);

        var ex = Record.Exception(() =>
        {
            vm.UpdateCellLayout(cellHeight: 0, cellWidth: 0);
            vm.UpdateCellLayout(cellHeight: 100, cellWidth: 0);
            vm.UpdateCellLayout(cellHeight: 0, cellWidth: 100);
        });

        Assert.Null(ex);
        // visibleCount must still be 1 (unchanged from default)
        Assert.Single(vm.Days[2].VisibleEvents);
    }

    [Fact]
    public void UpdateCellLayout_AffectsAllCurrentMonthCells()
    {
        // Populate multiple days with 2 events each. UpdateCellLayout must
        // update all of them, not just a subset.
        var adapter = new EventfulAdapter();
        for (int d = 1; d <= 5; d++)
        {
            adapter.Set(2082, 12, d, eventsEn: ["Ev1", "Ev2"]);
        }

        var vm = CreateAt12(adapter: adapter);

        // Before: all show 1 visible, 1 hidden
        for (int i = 2; i <= 6; i++) // cells 2-6 = days 1-5
        {
            Assert.True(vm.Days[i].HasHiddenEvents, $"Pre-check: cell[{i}] should have hidden events.");
        }

        vm.UpdateCellLayout(cellHeight: 200, cellWidth: 114); // visibleCount → 8

        for (int i = 2; i <= 6; i++)
        {
            Assert.False(vm.Days[i].HasHiddenEvents, $"After layout: cell[{i}] must show all events.");
        }
    }

    [Fact]
    public void UpdateCellLayout_PaddingCellsUnaffected()
    {
        var vm = CreateAt12();
        vm.UpdateCellLayout(cellHeight: 200, cellWidth: 114);

        var paddingCells = vm.Days.Where(d => d.IsPadding).ToList();
        Assert.All(paddingCells, d =>
        {
            Assert.Empty(d.VisibleEvents);
            Assert.False(d.HasVisibleEvents);
        });
    }

    [Fact]
    public void UpdateCellLayout_AfterNavigation_NewMonthCellsUseUpdatedCount()
    {
        // UpdateCellLayout is called once when the View renders. If navigation
        // happens after that, the new grid must use the same visible count.
        var adapter = new EventfulAdapter();
        // Set events on day 1 of 2082/11 (which is visible after PrevMonthCommand)
        adapter.Set(2082, 11, 1, eventsEn: ["A", "B", "C"]);
        var vm = CreateAt12(adapter: adapter);

        // Simulate View rendering with a large cell
        vm.UpdateCellLayout(cellHeight: 200, cellWidth: 114); // visibleCount = 8

        vm.PrevMonthCommand.Execute(null); // → 2082/11

        // Day 1 of 2082/11 is at cell[6] (leadingPad=6)
        Assert.Equal(3, vm.Days[6].VisibleEvents.Count);
        Assert.False(vm.Days[6].HasHiddenEvents);
    }

    // ════════════════════════════════════════════════════════════════════════
    // COMBINED SCENARIOS: simulate realistic human interaction sequences
    // ════════════════════════════════════════════════════════════════════════

    [Fact]
    public void HumanSequence_NavigateForwardCheckEventsComeBack()
    {
        // User opens widget, sees event on day 1, navigates forward 2 months,
        // navigates back 2 months. Events on day 1 of 2082/12 must still show.
        var adapter = new EventfulAdapter();
        adapter.Set(2082, 12, 1, eventsEn: ["Festival"]);
        var vm = CreateAt12(adapter: adapter);

        Assert.True(vm.Days[2].HasVisibleEvents);

        vm.NavigateMonths(2);  // → 2083/02
        vm.NavigateMonths(-2); // → back to 2082/12

        Assert.False(vm.Days[2].IsPadding, "Day 1 must be current-month after round-trip.");
        Assert.True(vm.Days[2].HasVisibleEvents, "Events must be restored after round-trip navigation.");
    }

    [Fact]
    public void HumanSequence_AddReminderThenNavigateAwayAndBack_DotPersists()
    {
        // User adds a reminder, navigates to next month (dot should be gone),
        // navigates back - dot should reappear.
        var rs = new ConfigurableReminderService();
        rs.SetReminders(2082, 12, 5);
        var vm = CreateAt12(rs: rs);

        Assert.True(vm.Days[6].HasReminders, "Precondition: day 5 reminder visible."); // cell[6]=day5

        vm.NextMonthCommand.Execute(null); // → 2083/01
        // day 5 of 2082/12 is no longer in view - reminder dot on current-month cells only
        var armedCells = vm.Days.Where(d => d.HasReminders).ToList();
        Assert.Empty(armedCells); // no reminders in 2083/01

        vm.PrevMonthCommand.Execute(null); // → back to 2082/12

        // cell[6] = day 5 of 2082/12 again
        Assert.True(vm.Days[6].HasReminders, "Reminder dot must reappear after navigating back.");
    }

    [Fact]
    public void HumanSequence_CollapseNavigateExpand_MonthLabelMatchesCurrentMonth()
    {
        // User navigates to Ashwin (month 6), collapses widget, reopens.
        // The MonthYearLabel must reflect month 6, not the initial month 12.
        var vm = CreateAt12();
        var (expand, collapse) = ViewLifecycle(vm);

        expand();
        vm.NavigateTo(2082, 6);
        string labelAtMonth6 = vm.MonthYearLabel;
        Assert.Contains("Ashwin", labelAtMonth6);

        collapse();
        expand(); // reopen

        Assert.Contains("Ashwin", vm.MonthYearLabel);
    }

    [Fact]
    public void HumanSequence_MultipleRemindersAndNotes_BothDotsCorrect()
    {
        // Day 3 has a reminder, day 7 has a note. Both dots must show simultaneously.
        var rs = new ConfigurableReminderService();
        rs.SetReminders(2082, 12, 3);
        var ns = new ConfigurableNotesService();
        ns.Seed(NotesService.FormatKey(2082, 12, 7), "Doctor appt");
        var vm = CreateAt12(rs: rs, ns: ns);

        // cell[4] = day 3, cell[8] = day 7
        Assert.True(vm.Days[4].HasReminders, "Day 3 must have reminder dot.");
        Assert.False(vm.Days[4].HasNote,     "Day 3 must not have note dot.");
        Assert.False(vm.Days[8].HasReminders, "Day 7 must not have reminder dot.");
        Assert.True(vm.Days[8].HasNote,       "Day 7 must have note dot.");
    }

    [Fact]
    public void HumanSequence_NavigateGoTodayNavigate_MonthCorrect()
    {
        // Navigate forward several months, go today, then navigate again.
        // Ensures GoToday resets the base for subsequent navigation correctly.
        var vm = CreateAt12();
        var (expand, _) = ViewLifecycle(vm);
        expand();

        vm.NavigateMonths(5);   // → 2083/05
        vm.GoTodayCommand.Execute(null); // → 2082/12 (today)
        vm.NavigateMonths(1);   // → 2083/01

        Assert.Equal(2083, vm.DisplayYear);
        Assert.Equal(1, vm.DisplayMonth);
    }

    [Fact]
    public void HumanSequence_LiveReminderUpdate_ThenNavigate_ThenBack_DotCorrect()
    {
        // Add a reminder via live update. Navigate away. Navigate back.
        // Dot must still be there (it was persisted in the service).
        var rs = new ConfigurableReminderService();
        var vm = CreateAt12(rs: rs);

        // Live add: arm day 10
        rs.SetReminders(2082, 12, 10);
        rs.FireChanged();

        Assert.True(vm.Days[11].HasReminders, "Day 10 dot must show after live update."); // cell[11]=day10

        vm.NextMonthCommand.Execute(null); // → 2083/01
        vm.PrevMonthCommand.Execute(null); // → back to 2082/12

        Assert.True(vm.Days[11].HasReminders, "Dot must persist after navigate-away-and-back.");
    }

    [Fact]
    public void HumanSequence_EventsThenNoEventsThenEventsAgain_CellStateConsistent()
    {
        // Navigate: 2082/12 (has events on day 1) → 2082/11 (day 1 = padding, no events)
        // → 2082/12 again (events restored). Tests that clearing and restoring works cleanly.
        var adapter = new EventfulAdapter();
        adapter.Set(2082, 12, 1, eventsEn: ["Dashain"]);
        var vm = CreateAt12(adapter: adapter);

        Assert.True(vm.Days[2].HasVisibleEvents, "Start: 2082/12 day 1 has event.");

        vm.PrevMonthCommand.Execute(null); // → 2082/11 (cell[2] = padding)
        Assert.True(vm.Days[2].IsPadding);
        Assert.False(vm.Days[2].HasVisibleEvents);

        vm.NextMonthCommand.Execute(null); // → back to 2082/12 (cell[2] = day 1)
        Assert.False(vm.Days[2].IsPadding, "Day 1 of 2082/12 must be current-month again.");
        Assert.True(vm.Days[2].HasVisibleEvents, "Events must be restored after returning to 2082/12.");
    }
}
