using NepDateWidget.Helpers;
using NepDateWidget.Models;
using System.Globalization;
using System.IO;
using System.Text.Json;
using System.Threading;

namespace NepDateWidget.Services;

public sealed class ReminderService : IReminderService, IDisposable
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
    };

    private readonly string _filePath;
    private readonly INepaliDateAdapter _adapter;
    private List<ReminderEntry> _reminders = new();
    private DebouncedFileReloader? _reloader;
    private long _lastSelfWriteTicks;

    private readonly SynchronizationContext? _syncContext;

    public event EventHandler? RemindersChanged;

    public ReminderService(string filePath, INepaliDateAdapter adapter)
    {
        _filePath = filePath ?? throw new ArgumentNullException(nameof(filePath));
        _adapter = adapter ?? throw new ArgumentNullException(nameof(adapter));
        _syncContext = SynchronizationContext.Current;
    }

    public IReadOnlyList<ReminderEntry> GetAll() => _reminders.AsReadOnly();

    public IReadOnlyList<ReminderEntry> GetForDate(int bsYear, int bsMonth, int bsDay)
        => _reminders.Where(r =>
        {
            var d = ReminderEntry.ParseDate(r.BsDate);
            return d is not null && d.Value.Year == bsYear && d.Value.Month == bsMonth && d.Value.Day == bsDay;
        }).ToList();

    public bool HasRemindersForDate(int bsYear, int bsMonth, int bsDay)
        => _reminders.Any(r =>
        {
            if (r.IsCompleted) return false;
            var d = ReminderEntry.ParseDate(r.BsDate);
            return d is not null && d.Value.Year == bsYear && d.Value.Month == bsMonth && d.Value.Day == bsDay;
        });

    public bool HasRemindersForDateExpanded(int bsYear, int bsMonth, int bsDay)
    {
        // Direct match first (fast path), excluding completed
        if (HasRemindersForDate(bsYear, bsMonth, bsDay))
            return true;

        // Check recurring reminders that would land on this date
        foreach (var r in _reminders)
        {
            if (r.IsCompleted) continue;
            if (r.Recurrence == ReminderRecurrence.None) continue;
            if (!WouldRecurOnDate(r, bsYear, bsMonth, bsDay)) continue;
            return true;
        }
        return false;
    }

    public IReadOnlyList<ReminderEntry> GetRecurringForDate(int bsYear, int bsMonth, int bsDay)
    {
        var results = new List<ReminderEntry>();
        foreach (var r in _reminders)
        {
            if (r.IsCompleted) continue;
            if (r.Recurrence == ReminderRecurrence.None) continue;
            // Skip if already an exact match (those are returned by GetForDate)
            var d = ReminderEntry.ParseDate(r.BsDate);
            if (d is not null && d.Value.Year == bsYear && d.Value.Month == bsMonth && d.Value.Day == bsDay) continue;
            if (WouldRecurOnDate(r, bsYear, bsMonth, bsDay))
                results.Add(r);
        }
        return results;
    }

    public void Add(ReminderEntry entry)
    {
        if (entry is null) return;
        if (string.IsNullOrWhiteSpace(entry.Title)) return;
        if (entry.Notes?.Length > 500)
            entry.Notes = entry.Notes[..500];
        // Preserve original date at creation for accurate recurrence computation
        entry.OriginalBsDate = entry.BsDate;
        _reminders.Add(entry);
        Save();
        RemindersChanged?.Invoke(this, EventArgs.Empty);
    }

    public void Update(ReminderEntry entry)
    {
        if (entry is null) return;
        var idx = _reminders.FindIndex(r => r.Id == entry.Id);
        if (idx < 0) return;
        if (entry.Notes?.Length > 500)
            entry.Notes = entry.Notes[..500];
        // If the user changed the date via edit, update the original date too
        // so recurrence calculations stay anchored to the new base date
        var old = _reminders[idx];
        if (old.BsDate != entry.BsDate)
            entry.OriginalBsDate = entry.BsDate;
        _reminders[idx] = entry;
        Save();
        RemindersChanged?.Invoke(this, EventArgs.Empty);
    }

    public void Delete(string id)
    {
        var removed = _reminders.RemoveAll(r => r.Id == id);
        if (removed > 0)
        {
            Save();
            RemindersChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public void Load()
    {
        if (!File.Exists(_filePath))
        {
            _reminders = new List<ReminderEntry>();
            Save();
        }
        else
        {
            LoadFromDisk();
        }
        _reloader ??= new DebouncedFileReloader(_filePath, debounceMs: 500, onReload: () =>
        {
            var elapsed = TimeSpan.FromTicks(DateTime.UtcNow.Ticks - Interlocked.Read(ref _lastSelfWriteTicks));
            if (elapsed.TotalSeconds < 1.0) return;
            LoadFromDisk();
            if (_syncContext is not null)
                _syncContext.Post(_ => RemindersChanged?.Invoke(this, EventArgs.Empty), null);
            else
                RemindersChanged?.Invoke(this, EventArgs.Empty);
        });
    }

    private void LoadFromDisk()
    {
        if (!File.Exists(_filePath))
        {
            _reminders = new List<ReminderEntry>();
            return;
        }

        try
        {
            var json = File.ReadAllText(_filePath);
            _reminders = JsonSerializer.Deserialize<List<ReminderEntry>>(json, SerializerOptions)
                         ?? new List<ReminderEntry>();

            // Backward compatibility: migrate entries from old 6-int format
            bool migrated = false;
            foreach (var r in _reminders)
            {
                if (string.IsNullOrEmpty(r.BsDate) && r.ExtensionData is not null)
                {
                    r.MigrateFromLegacyIfNeeded();
                    migrated = true;
                }
                // Ensure OriginalBsDate is populated
                if (string.IsNullOrEmpty(r.OriginalBsDate) && !string.IsNullOrEmpty(r.BsDate))
                {
                    r.OriginalBsDate = r.BsDate;
                    migrated = true;
                }
            }
            if (migrated) Save();
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            Log.Warn($"Failed to load reminders: {ex.Message}");
            _reminders = new List<ReminderEntry>();
        }
    }

    public void Save()
    {
        Interlocked.Exchange(ref _lastSelfWriteTicks, DateTime.UtcNow.Ticks);
        try
        {
            var json = JsonSerializer.Serialize(_reminders, SerializerOptions);
            // Route through AtomicFile so reminders share the same write/swap/cleanup
            // contract as settings.json and notes.json. AtomicFile is best-effort:
            // a returned false is logged but never thrown so the app stays alive.
            if (!AtomicFile.WriteAllText(_filePath, json))
                Log.Warn("Failed to save reminders: AtomicFile.WriteAllText returned false");
        }
        catch (JsonException ex)
        {
            Log.Warn($"Failed to serialize reminders: {ex.Message}");
        }
    }

    public void Dispose() => _reloader?.Dispose();

    // Only fire a notification if the reminder is due within this window.
    // Anything older is silently completed or advanced without showing a popup.
    private static readonly TimeSpan FireGracePeriod = TimeSpan.FromMinutes(2);

    public IReadOnlyList<ReminderEntry> CheckAndFireDueReminders(DateTime nowUtc)
    {
        var fired = new List<ReminderEntry>();
        var toRemove = new List<string>();
        bool changed = false;

        foreach (var r in _reminders.ToList())
        {
            if (r.IsCompleted) continue;

            // Check if end date has passed for recurring reminders
            if (r.Recurrence != ReminderRecurrence.None && !string.IsNullOrEmpty(r.EndDate))
            {
                if (IsEndDatePassed(r.EndDate))
                {
                    toRemove.Add(r.Id);
                    continue;
                }
            }

            var nextFireUtc = GetNextFireTimeUtc(r);
            if (nextFireUtc is null) continue;
            if (nextFireUtc.Value > nowUtc) continue; // not yet due

            // A reminder is stale if its fire time is more than the grace period ago.
            // This covers both past dates AND past times on today's date.
            bool isStale = (nowUtc - nextFireUtc.Value) > FireGracePeriod;

            if (r.Recurrence == ReminderRecurrence.None)
            {
                if (!isStale)
                    fired.Add(r);
                r.LastFiredUtc = nowUtc;
                r.IsCompleted = true;
                changed = true;
            }
            else
            {
                if (!isStale)
                {
                    fired.Add(r);
                    r.LastFiredUtc = nowUtc;
                }

                if (!AdvanceToNextOccurrence(r))
                {
                    r.IsCompleted = true;
                }
                else if (isStale)
                {
                    // Fast-forward recurring reminders that fell behind
                    CatchUpRecurring(r, nowUtc);
                }
                changed = true;
            }
        }

        if (toRemove.Count > 0)
            _reminders.RemoveAll(r => toRemove.Contains(r.Id));

        if (fired.Count > 0 || toRemove.Count > 0 || changed)
        {
            Save();
            RemindersChanged?.Invoke(this, EventArgs.Empty);
        }

        return fired;
    }

    /// <summary>
    /// Advances a recurring reminder through all missed occurrences until its next fire
    /// time is in the future (or the reminder exhausts its end date / calendar range).
    /// </summary>
    private void CatchUpRecurring(ReminderEntry r, DateTime nowUtc)
    {
        for (int i = 0; i < 1000; i++)
        {
            var next = GetNextFireTimeUtc(r);
            if (next is null || next.Value > nowUtc) break;

            if (!string.IsNullOrEmpty(r.EndDate) && IsEndDatePassed(r.EndDate))
            {
                r.IsCompleted = true;
                break;
            }

            if (!AdvanceToNextOccurrence(r))
            {
                r.IsCompleted = true;
                break;
            }
        }
    }

    public IReadOnlyList<ReminderEntry> GetMissedReminders()
    {
        var missed = new List<ReminderEntry>();
        var nowUtc = DateTime.UtcNow;

        foreach (var r in _reminders)
        {
            if (r.IsCompleted) continue;
            var nextFire = GetNextFireTimeUtc(r);
            if (nextFire is null) continue;
            if (nextFire.Value <= nowUtc)
                missed.Add(r);
        }

        return missed;
    }

    private DateTime? GetNextFireTimeUtc(ReminderEntry r)
    {
        if (!TimeSpan.TryParse(r.Time, CultureInfo.InvariantCulture, out var timeOfDay))
            return null;

        var bsParts = ReminderEntry.ParseDate(r.BsDate);
        if (bsParts is null) return null;

        var adDate = _adapter.BsToAd(bsParts.Value.Year, bsParts.Value.Month, bsParts.Value.Day);
        if (adDate is null) return null;

        // Explicitly mark as local so ToUniversalTime converts correctly
        // regardless of what DateTimeKind the adapter returns.
        var localDateTime = DateTime.SpecifyKind(adDate.Value.Date + timeOfDay, DateTimeKind.Local);
        return localDateTime.ToUniversalTime();
    }

    private bool AdvanceToNextOccurrence(ReminderEntry r)
    {
        var bsParts = ReminderEntry.ParseDate(r.BsDate);
        if (bsParts is null) return false;
        int y = bsParts.Value.Year, m = bsParts.Value.Month, d = bsParts.Value.Day;

        switch (r.Recurrence)
        {
            case ReminderRecurrence.Daily:
            {
                var next = _adapter.AddDays(y, m, d, 1);
                if (next is null) return false;
                r.BsDate = ReminderEntry.FormatDate(next.Value.Year, next.Value.Month, next.Value.Day);
                return true;
            }
            case ReminderRecurrence.Weekly:
            {
                var next = _adapter.AddDays(y, m, d, 7);
                if (next is null) return false;
                r.BsDate = ReminderEntry.FormatDate(next.Value.Year, next.Value.Month, next.Value.Day);
                return true;
            }
            case ReminderRecurrence.Monthly:
            {
                // Preserve the original intended day so months with fewer days don't
                // permanently drift to a lower day (e.g. a 32nd-day reminder stays at
                // 32 intent, clamped per month, not locked to the first clamped value).
                var origParts = ReminderEntry.ParseDate(r.OriginalBsDate);
                int origDay = origParts?.Day ?? d;
                int newM = m + 1, newY = y;
                if (newM > 12) { newM = 1; newY++; }
                int maxDay = _adapter.GetDaysInMonth(newY, newM);
                if (maxDay <= 0) return false;
                r.BsDate = ReminderEntry.FormatDate(newY, newM, Math.Min(origDay, maxDay));
                return true;
            }
            case ReminderRecurrence.Yearly:
            {
                var origParts = ReminderEntry.ParseDate(r.OriginalBsDate);
                int origMonth = origParts?.Month ?? m;
                int origDay   = origParts?.Day   ?? d;
                int newY = y + 1;
                int maxDay = _adapter.GetDaysInMonth(newY, origMonth);
                if (maxDay <= 0) return false;
                r.BsDate = ReminderEntry.FormatDate(newY, origMonth, Math.Min(origDay, maxDay));
                return true;
            }
            default:
                return false;
        }
    }

    private bool IsEndDatePassed(string endDateStr)
    {
        var parts = ReminderEntry.ParseDate(endDateStr);
        if (parts is null) return false;

        var endAd = _adapter.BsToAd(parts.Value.Year, parts.Value.Month, parts.Value.Day);
        if (endAd is null) return true; // invalid end date = treat as passed
        return DateTime.Now.Date > endAd.Value.Date;
    }

    /// <summary>
    /// Checks whether a recurring reminder would occur on the target date.
    /// Uses O(1) math for Daily/Weekly, simulation from original date for Monthly.
    /// </summary>
    private bool WouldRecurOnDate(ReminderEntry r, int targetY, int targetM, int targetD)
    {
        if (r.IsCompleted) return false;

        // Use original date for recurrence base (avoids drift from AdvanceToNextOccurrence)
        var origParts = ReminderEntry.ParseDate(r.OriginalBsDate);
        if (origParts is null)
        {
            origParts = ReminderEntry.ParseDate(r.BsDate);
            if (origParts is null) return false;
        }
        int origY = origParts.Value.Year, origM = origParts.Value.Month, origD = origParts.Value.Day;

        var targetAd = _adapter.BsToAd(targetY, targetM, targetD);
        var sourceAd = _adapter.BsToAd(origY, origM, origD);
        if (targetAd is null || sourceAd is null) return false;
        if (targetAd.Value.Date < sourceAd.Value.Date) return false;

        // Check end date: if target is past the end date, no recurrence
        if (!string.IsNullOrEmpty(r.EndDate))
        {
            var endParts = ReminderEntry.ParseDate(r.EndDate);
            if (endParts is not null)
            {
                var endAd = _adapter.BsToAd(endParts.Value.Year, endParts.Value.Month, endParts.Value.Day);
                if (endAd is not null && targetAd.Value.Date > endAd.Value.Date)
                    return false;
            }
        }

        int daysDiff = (targetAd.Value.Date - sourceAd.Value.Date).Days;

        switch (r.Recurrence)
        {
            case ReminderRecurrence.Daily:
                // Every day after the original date is a valid occurrence
                return true;

            case ReminderRecurrence.Weekly:
                // Every 7 AD days from the original date
                return daysDiff % 7 == 0;

            case ReminderRecurrence.Monthly:
                // Monthly is complex due to BS month day count variations (day clamping).
                // Simulate from original date using AddMonths(orig, n) to avoid drift.
                return WouldRecurMonthlyOnDate(origY, origM, origD, targetY, targetM, targetD, targetAd.Value);

            case ReminderRecurrence.Yearly:
                // Same month every year, day clamped to what that month supports.
                if (targetM != origM) return false;
                if (targetY <= origY) return false;
                int maxDayY = _adapter.GetDaysInMonth(targetY, origM);
                if (maxDayY <= 0) return false;
                return targetD == Math.Min(origD, maxDayY);

            default:
                return false;
        }
    }

    private bool WouldRecurMonthlyOnDate(int origY, int origM, int origD,
                                          int targetY, int targetM, int targetD,
                                          DateTime targetAd)
    {
        for (int n = 1; n <= 36; n++)
        {
            var next = _adapter.AddMonths(origY, origM, origD, n);
            if (next is null) break;

            if (next.Value.Year == targetY && next.Value.Month == targetM && next.Value.Day == targetD)
                return true;

            // Overshot the target date: stop early
            var nextAd = _adapter.BsToAd(next.Value.Year, next.Value.Month, next.Value.Day);
            if (nextAd is not null && nextAd.Value.Date > targetAd.Date)
                break;
        }
        return false;
    }
}
