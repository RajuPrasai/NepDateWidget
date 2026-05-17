using NepDateWidget.Models;

namespace NepDateWidget.Services;

/// <summary>
/// Walks the BS calendar day-by-day via <see cref="INepaliDateAdapter"/> to find
/// upcoming public holidays. Results are cached per AD-date; cache invalidates
/// automatically on the first call after the system date changes (midnight
/// rollover, manual clock change, or sleep across midnight).
/// </summary>
public sealed class HolidayLookupService : IHolidayLookupService
{
    /// <summary>
    /// Hard cap on how far ahead we walk before giving up. NepDate calendar
    /// metadata covers BS 2001-2089 (~88 years), and there is at least one
    /// public holiday every few weeks across that span, so 5 years is a very
    /// safe upper bound that still terminates quickly if the user sits on a
    /// date past the supported metadata range.
    /// </summary>
    private const int MaxLookaheadDays = 1825;

    /// <summary>
    /// Tooltip enumeration cap. Walks one calendar year forward by default,
    /// which gives the user a complete "next 12 months" view without spending
    /// time on entries that are too far away to be actionable.
    /// </summary>
    private const int DefaultTooltipWindowDays = 366;

    private readonly INepaliDateAdapter _adapter;
    private readonly object _gate = new();

    private DateTime _cacheAdDate = DateTime.MinValue;
    private UpcomingHoliday? _cachedNext;
    private List<UpcomingHoliday>? _cachedUpcoming;
    private int _cachedUpcomingMaxCount;

    public HolidayLookupService(INepaliDateAdapter adapter)
    {
        _adapter = adapter ?? throw new ArgumentNullException(nameof(adapter));
    }

    public UpcomingHoliday? GetNextHoliday()
    {
        lock (_gate)
        {
            EnsureCacheFresh();
            if (_cachedNext is not null)
            {
                return _cachedNext;
            }

            _cachedNext = WalkForFirstHoliday();
            return _cachedNext;
        }
    }

    public IReadOnlyList<UpcomingHoliday> GetUpcomingHolidays(int maxCount)
    {
        if (maxCount <= 0)
        {
            return Array.Empty<UpcomingHoliday>();
        }

        lock (_gate)
        {
            EnsureCacheFresh();
            if (_cachedUpcoming is not null && _cachedUpcomingMaxCount >= maxCount)
            {
                return _cachedUpcoming.Take(maxCount).ToList();
            }

            _cachedUpcoming = WalkForUpcoming(maxCount);
            _cachedUpcomingMaxCount = maxCount;
            return _cachedUpcoming;
        }
    }

    public void InvalidateCache()
    {
        lock (_gate)
        {
            _cacheAdDate = DateTime.MinValue;
            _cachedNext = null;
            _cachedUpcoming = null;
            _cachedUpcomingMaxCount = 0;
        }
    }

    // ── Internals ─────────────────────────────────────────────────────────────

    private void EnsureCacheFresh()
    {
        var today = _adapter.GetTodayAd().Date;
        if (today != _cacheAdDate)
        {
            _cacheAdDate = today;
            _cachedNext = null;
            _cachedUpcoming = null;
            _cachedUpcomingMaxCount = 0;
        }
    }

    private UpcomingHoliday? WalkForFirstHoliday()
    {
        var (y, m, d) = _adapter.GetTodayBs();

        for (int offset = 0; offset <= MaxLookaheadDays; offset++)
        {
            int cy, cm, cd;
            if (offset == 0) { cy = y; cm = m; cd = d; }
            else
            {
                var next = _adapter.AddDays(y, m, d, offset);
                if (next is null)
                {
                    return null;
                }

                (cy, cm, cd) = next.Value;
            }

            var info = _adapter.GetCalendarInfo(cy, cm, cd);
            if (info.IsPublicHoliday)
            {
                return BuildHoliday(cy, cm, cd, offset, info.EventsEn, info.EventsNp);
            }
        }
        return null;
    }

    private List<UpcomingHoliday> WalkForUpcoming(int maxCount)
    {
        var result = new List<UpcomingHoliday>(Math.Min(maxCount, 16));
        var (y, m, d) = _adapter.GetTodayBs();

        // Tooltip walks one year by default but at least far enough to cover
        // the requested count; never further than the hard lookahead cap.
        int window = Math.Min(MaxLookaheadDays, Math.Max(DefaultTooltipWindowDays, maxCount * 90));

        for (int offset = 0; offset <= window && result.Count < maxCount; offset++)
        {
            int cy, cm, cd;
            if (offset == 0) { cy = y; cm = m; cd = d; }
            else
            {
                var next = _adapter.AddDays(y, m, d, offset);
                if (next is null)
                {
                    break;
                }

                (cy, cm, cd) = next.Value;
            }

            var info = _adapter.GetCalendarInfo(cy, cm, cd);
            if (info.IsPublicHoliday)
            {
                result.Add(BuildHoliday(cy, cm, cd, offset, info.EventsEn, info.EventsNp));
            }
        }
        return result;
    }

    private UpcomingHoliday BuildHoliday(
        int bsYear, int bsMonth, int bsDay, int daysUntil,
        string[] eventsEn, string[] eventsNp)
    {
        return new UpcomingHoliday
        {
            BsYear = bsYear,
            BsMonth = bsMonth,
            BsDay = bsDay,
            DaysUntil = daysUntil,
            NamesEn = NormalizeNames(eventsEn),
            NamesNp = NormalizeNames(eventsNp),
            BsLongEn = _adapter.FormatBsLongEn(bsYear, bsMonth, bsDay),
            BsLongNp = _adapter.FormatBsLongNe(bsYear, bsMonth, bsDay),
        };
    }

    private static IReadOnlyList<string> NormalizeNames(string[] events)
    {
        if (events is null || events.Length == 0)
        {
            return Array.Empty<string>();
        }

        var list = new List<string>(events.Length);
        for (int i = 0; i < events.Length; i++)
        {
            if (string.IsNullOrWhiteSpace(events[i]))
            {
                continue;
            }

            list.Add(events[i].Trim());
        }
        return list.Count == 0 ? Array.Empty<string>() : list;
    }
}
