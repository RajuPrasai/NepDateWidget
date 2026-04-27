using System.Globalization;
using NepDateWidget.Models;
using NepDateWidget.Services;

namespace NepDateWidget.Helpers;

/// <summary>
/// Pure helper that builds the list of clipboard format options for a single
/// calendar day. Kept free of WPF dependencies so it can be unit-tested without
/// instantiating the UI.
///
/// The set of options is intentionally fixed (BS short / BS long / AD ISO / AD
/// long) so the right-click menu stays predictable. Each option's <c>Header</c>
/// is the formatted date itself, since the menu carries a separate "Copy"
/// title row that already explains the action.
/// </summary>
internal static class DateFormatter
{
    /// <summary>
    /// Builds copy options for a current-month day. Returns an empty list when
    /// the BS date is outside the adapter's supported range (any single format
    /// failure short-circuits to empty so the menu stays consistent rather than
    /// showing a partial set).
    /// </summary>
    public static IReadOnlyList<DateFormatOption> Build(
        int bsYear, int bsMonth, int bsDay,
        INepaliDateAdapter adapter,
        ILocalizationService loc,
        bool isNepali)
    {
        if (adapter is null) throw new ArgumentNullException(nameof(adapter));
        if (loc is null)     throw new ArgumentNullException(nameof(loc));

        // BS short / long, language-aware
        string bsShort = isNepali
            ? adapter.FormatBsShortNe(bsYear, bsMonth, bsDay)
            : adapter.FormatBsShortEn(bsYear, bsMonth, bsDay);

        string bsLong = isNepali
            ? adapter.FormatBsLongNe(bsYear, bsMonth, bsDay)
            : adapter.FormatBsLongEn(bsYear, bsMonth, bsDay);

        // AD always Arabic digits, English month names: clipboard targets are
        // typically external apps (browsers, editors, spreadsheets) where ASCII
        // is the safe default. Localizing AD digits would mostly create paste
        // problems.
        var ad = adapter.BsToAd(bsYear, bsMonth, bsDay);

        // If any required formatter returned empty (out-of-range BS date), skip
        if (string.IsNullOrEmpty(bsShort) || string.IsNullOrEmpty(bsLong) || !ad.HasValue)
            return Array.Empty<DateFormatOption>();

        string adIso  = ad.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        string adLong = ad.Value.ToString("d MMM yyyy", CultureInfo.InvariantCulture);

        return new[]
        {
            new DateFormatOption("bs_short", bsShort, bsShort),
            new DateFormatOption("bs_long",  bsLong,  bsLong),
            new DateFormatOption("ad_iso",   adIso,   adIso),
            new DateFormatOption("ad_long",  adLong,  adLong),
        };
    }
}
