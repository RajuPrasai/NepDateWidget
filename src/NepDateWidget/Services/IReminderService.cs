using NepDateWidget.Models;

namespace NepDateWidget.Services;

public interface IReminderService
{
    IReadOnlyList<ReminderEntry> GetAll();
    IReadOnlyList<ReminderEntry> GetForDate(int bsYear, int bsMonth, int bsDay);
    bool HasRemindersForDate(int bsYear, int bsMonth, int bsDay);

    /// <summary>
    /// Returns true if any reminder (direct or expanded recurring) falls on the given date.
    /// Expands recurring reminders up to the given date for dot display.
    /// </summary>
    bool HasRemindersForDateExpanded(int bsYear, int bsMonth, int bsDay);

    /// <summary>
    /// Returns recurring reminders whose recurrence pattern lands on the given date
    /// but whose stored date is different. Used to populate the popup list.
    /// </summary>
    IReadOnlyList<ReminderEntry> GetRecurringForDate(int bsYear, int bsMonth, int bsDay);

    void Add(ReminderEntry entry);
    void Update(ReminderEntry entry);
    void Delete(string id);
    void Load();
    void Save();

    /// <summary>
    /// Returns reminders that should have fired since the last check but were missed
    /// (app was closed or timer skipped). Marks them as fired.
    /// </summary>
    IReadOnlyList<ReminderEntry> CheckAndFireDueReminders(DateTime nowUtc);

    /// <summary>
    /// Returns reminders whose scheduled time has passed and were never fired.
    /// Used on startup for badge count.
    /// </summary>
    IReadOnlyList<ReminderEntry> GetMissedReminders();

    /// <summary>Fired when reminders change (add/update/delete/auto-cleanup).</summary>
    event EventHandler? RemindersChanged;

    /// <summary>
    /// Returns a set of BS day numbers (1-based) within the given BS month that have
    /// at least one reminder (direct or expanded recurring). One pass over all reminders;
    /// avoids the O(cells × reminders) per-cell query pattern for calendar dot rendering.
    /// </summary>
    HashSet<int> GetHasRemindersForMonth(int bsYear, int bsMonth);
}
