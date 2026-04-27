using NepDateWidget.Helpers;
using System.Windows.Input;

namespace NepDateWidget.ViewModels;

/// <summary>
/// View model for a single interest rate row in the Interest Calculator.
/// The first row's <see cref="FromDate"/> mirrors the global From date and is read-only in the View.
/// Subsequent rows have an editable FromDate (auto-filled to next month's first day when added).
/// </summary>
public sealed class InterestRateRowViewModel : ViewModelBase
{
    /// <summary>True for the first row - its FromDate is read-only and always mirrors the global From date.</summary>
    public bool IsFirstRow { get; }

    private string _fromDate;
    public string FromDate
    {
        get => _fromDate;
        set
        {
            if (SetProperty(ref _fromDate, value))
                Changed?.Invoke(this, EventArgs.Empty);
        }
    }

    private string _rate;
    public string Rate
    {
        get => _rate;
        set
        {
            if (SetProperty(ref _rate, value))
                Changed?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>Command to remove this row (hidden in the View for the first row).</summary>
    public ICommand RemoveCommand { get; }

    /// <summary>Fires when any field on this row changes (used by parent VM to clear results).</summary>
    public event EventHandler? Changed;

    public InterestRateRowViewModel(
        string fromDate,
        string rate,
        bool isFirstRow,
        Action<InterestRateRowViewModel> onRemove)
    {
        _fromDate   = fromDate;
        _rate       = rate;
        IsFirstRow  = isFirstRow;
        RemoveCommand = new RelayCommand(() => onRemove(this));
    }

    /// <summary>
    /// Silently updates FromDate without firing the <see cref="Changed"/> event.
    /// Called when the global From date changes so the first row's display stays in sync
    /// without triggering a result-clear loop.
    /// </summary>
    public void SyncFromDate(string date)
    {
        if (_fromDate == date) return;
        _fromDate = date;
        OnPropertyChanged(nameof(FromDate));
    }
}
