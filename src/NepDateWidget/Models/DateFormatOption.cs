namespace NepDateWidget.Models;

/// <summary>
/// One row in the day-cell "copy date" context menu.
/// Built once per <see cref="ViewModels.CalendarDayViewModel"/>.
/// Carries everything the menu needs to render and act:
///   <see cref="Header"/>   - text shown to the user (label + preview)
///   <see cref="Value"/>    - the exact text that will be placed on the clipboard
///   <see cref="Key"/>      - stable identifier for logging and tests
/// </summary>
public sealed record DateFormatOption(string Key, string Header, string Value);
