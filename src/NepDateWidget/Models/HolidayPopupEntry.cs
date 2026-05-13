namespace NepDateWidget.Models;

/// <summary>
/// One row in the holiday-countdown popup: a single named event together with
/// the BS date it falls on and a short relative-time hint (today/tomorrow/in N
/// days). Multiple events on the same day produce multiple entries that share
/// the same <see cref="WhenLabel"/> and <see cref="DateLabel"/>.
/// </summary>
public sealed class HolidayPopupEntry
{
    public string Name      { get; }
    public string DateLabel { get; }
    public string WhenLabel { get; }
    public bool   IsToday   { get; }

    public HolidayPopupEntry(string name, string dateLabel, string whenLabel, bool isToday)
    {
        Name      = name;
        DateLabel = dateLabel;
        WhenLabel = whenLabel;
        IsToday   = isToday;
    }
}
