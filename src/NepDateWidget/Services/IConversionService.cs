using NepDateWidget.Models;

namespace NepDateWidget.Services;

/// <summary>
/// Converts between AD (Gregorian) and BS (Bikram Sambat) dates.
/// All conversion errors are returned as <see cref="ConversionResult.Fail"/> values -
/// no exceptions escape this service.
/// </summary>
public interface IConversionService
{
    /// <summary>
    /// Converts an AD (Gregorian) date to BS.
    /// </summary>
    /// <param name="adYear">Gregorian year.</param>
    /// <param name="adMonth">Gregorian month (1–12).</param>
    /// <param name="adDay">Gregorian day.</param>
    ConversionResult AdToBs(int adYear, int adMonth, int adDay);

    /// <summary>
    /// Converts a BS (Bikram Sambat) date to AD.
    /// </summary>
    /// <param name="bsYear">BS year (1901–2199).</param>
    /// <param name="bsMonth">BS month (1–12).</param>
    /// <param name="bsDay">BS day.</param>
    ConversionResult BsToAd(int bsYear, int bsMonth, int bsDay);

    /// <summary>
    /// Parses a date string (e.g. "2081-01-15", "2081/01/15", "2024-3-5")
    /// and converts in the specified direction.
    /// </summary>
    ConversionResult ConvertFromText(string input, bool isAdToBs);
}
