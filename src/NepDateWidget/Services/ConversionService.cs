using NepDateWidget.Models;

namespace NepDateWidget.Services;

/// <summary>
/// Converts between AD (Gregorian) and BS (Bikram Sambat) dates.
/// All errors are returned as <see cref="ConversionResult.Fail"/> - nothing throws.
/// </summary>
public sealed class ConversionService : IConversionService
{
    private readonly INepaliDateAdapter _adapter;

    public ConversionService(INepaliDateAdapter adapter)
    {
        _adapter = adapter ?? throw new ArgumentNullException(nameof(adapter));
    }

    // ── IConversionService ────────────────────────────────────────────────────

    public ConversionResult AdToBs(int adYear, int adMonth, int adDay)
    {
        DateTime adDate;
        try
        {
            adDate = new DateTime(adYear, adMonth, adDay);
        }
        catch (ArgumentOutOfRangeException)
        {
            return ConversionResult.Fail("The entered date is not a valid Gregorian date.");
        }

        var result = _adapter.AdToBs(adDate);
        if (result is null)
            return ConversionResult.Fail("The AD date is outside the supported BS range (1901–2199).");

        var (bsYear, bsMonth, bsDay) = result.Value;
        return ConversionResult.Ok(
            result: $"{bsYear:D4}/{bsMonth:D2}/{bsDay:D2}",
            resultLong: _adapter.FormatBsLongEn(bsYear, bsMonth, bsDay));
    }

    public ConversionResult BsToAd(int bsYear, int bsMonth, int bsDay)
    {
        // NepDate validates internally; BsToAd returns null for invalid/out-of-range dates
        var adDate = _adapter.BsToAd(bsYear, bsMonth, bsDay);
        if (adDate is null)
            return ConversionResult.Fail("The BS date is invalid or outside the supported range.");

        return ConversionResult.Ok(
            result: adDate.Value.ToString("yyyy-MM-dd"),
            resultLong: adDate.Value.ToString("MMMM d, yyyy"));
    }

    public ConversionResult ConvertFromText(string input, bool isAdToBs)
    {
        if (string.IsNullOrWhiteSpace(input))
            return ConversionResult.Fail("Please enter a date.");

        var trimmed = input.Trim();

        if (isAdToBs)
        {
            // AD → BS: try YYYY-MM-DD split first, then flexible DateTime.Parse
            var parts = trimmed.Split(new[] { '-', '/', '.', ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 3
                && int.TryParse(parts[0], out int year)
                && int.TryParse(parts[1], out int month)
                && int.TryParse(parts[2], out int day))
            {
                return AdToBs(year, month, day);
            }

            if (DateTime.TryParse(trimmed, System.Globalization.CultureInfo.InvariantCulture,
                    System.Globalization.DateTimeStyles.None, out DateTime parsed))
            {
                return AdToBs(parsed.Year, parsed.Month, parsed.Day);
            }

            return ConversionResult.Fail("Enter an AD date like 2024-04-15");
        }
        else
        {
            // BS → AD: use SmartDateParser (handles Nepali digits, month names, alternate separators)
            // with autoAdjust TryParse as a fallback for numeric-only edge cases.
            if (!_adapter.TryParseSmartBsDate(trimmed, out int year, out int month, out int day))
                return ConversionResult.Fail("Enter a BS date like 2081/01/15");
            return BsToAd(year, month, day);
        }
    }
}
