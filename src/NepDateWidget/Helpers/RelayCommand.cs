using System.Windows.Input;

namespace NepDateWidget.Helpers;

/// <summary>
/// Minimal ICommand implementation backed by delegates.
/// When a canExecute delegate is provided, CanExecuteChanged is wired to
/// CommandManager.RequerySuggested so WPF re-evaluates automatically.
/// When no canExecute is provided, CanExecute always returns true and
/// RequerySuggested is not hooked — avoiding needless CanExecute calls on
/// every mouse move across all 9 cached tab visual trees.
/// </summary>
public sealed class RelayCommand : ICommand
{
    private readonly Action _execute;
    private readonly Func<bool>? _canExecute;

    public RelayCommand(Action execute, Func<bool>? canExecute = null)
    {
        _execute = execute ?? throw new ArgumentNullException(nameof(execute));
        _canExecute = canExecute;
    }

    public event EventHandler? CanExecuteChanged
    {
        add    { if (_canExecute is not null) CommandManager.RequerySuggested += value; }
        remove { if (_canExecute is not null) CommandManager.RequerySuggested -= value; }
    }

    public bool CanExecute(object? parameter) => _canExecute?.Invoke() ?? true;

    public void Execute(object? parameter) => _execute();
}

/// <summary>
/// Generic variant that passes a typed parameter to the execute and canExecute delegates.
/// Same RequerySuggested opt-in rule as <see cref="RelayCommand"/>.
/// </summary>
public sealed class RelayCommand<T> : ICommand
{
    private readonly Action<T?> _execute;
    private readonly Func<T?, bool>? _canExecute;

    public RelayCommand(Action<T?> execute, Func<T?, bool>? canExecute = null)
    {
        _execute = execute ?? throw new ArgumentNullException(nameof(execute));
        _canExecute = canExecute;
    }

    public event EventHandler? CanExecuteChanged
    {
        add    { if (_canExecute is not null) CommandManager.RequerySuggested += value; }
        remove { if (_canExecute is not null) CommandManager.RequerySuggested -= value; }
    }

    public bool CanExecute(object? parameter) => _canExecute?.Invoke((T?)parameter) ?? true;

    public void Execute(object? parameter) => _execute((T?)parameter);
}
