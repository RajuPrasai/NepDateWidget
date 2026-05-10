using NepDateWidget.Helpers;
using NepDateWidget.Services;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
using System.Windows.Input;
using System.Windows.Threading;

namespace NepDateWidget.ViewModels;

public sealed class RunBoxViewModel : ViewModelBase
{
    private readonly ISettingsService _settingsService;
    private readonly ILocalizationService _loc;
    private readonly List<string> _history;
    private readonly DispatcherTimer _errorTimer;

    private string _runText = string.Empty;
    public string RunText
    {
        get => _runText;
        set
        {
            if (SetProperty(ref _runText, value))
            {
                UpdateFilteredHistory();
                // While the user is typing, auto-highlight the best match so Enter executes it.
                // Clearing the filtered list above resets the ListBox's internal selection,
                // so we must always go through -1 before re-selecting 0; otherwise the
                // OneWay binding sees no change and the highlight is dropped on the next keystroke.
                SelectedHistoryIndex = -1;
                if (_isHistoryOpen && FilteredHistory.Count > 0)
                    SelectedHistoryIndex = 0;
            }
        }
    }

    private string _errorText = string.Empty;
    public string ErrorText
    {
        get => _errorText;
        private set => SetProperty(ref _errorText, value);
    }

    private bool _showError;
    public bool ShowError
    {
        get => _showError;
        private set => SetProperty(ref _showError, value);
    }

    private bool _isHistoryOpen;
    public bool IsHistoryOpen
    {
        get => _isHistoryOpen;
        set => SetProperty(ref _isHistoryOpen, value);
    }

    private int _selectedHistoryIndex = -1;
    public int SelectedHistoryIndex
    {
        get => _selectedHistoryIndex;
        set => SetProperty(ref _selectedHistoryIndex, value);
    }

    public ObservableCollection<string> FilteredHistory { get; } = new();

    // Labels
    public string PlaceholderLabel { get; private set; } = string.Empty;

    // Commands
    public ICommand ExecuteCommand { get; }
    public ICommand SelectHistoryItemCommand { get; }
    public ICommand RemoveHistoryItemCommand { get; }
    public ICommand ClearErrorCommand { get; }

    /// <summary>
    /// Raised when the RunBox requests the widget to collapse (Escape key).
    /// </summary>
    public event EventHandler? CollapseRequested;

    public RunBoxViewModel(ISettingsService settingsService, ILocalizationService localizationService)
    {
        _settingsService = settingsService;
        _loc = localizationService;
        _history = _settingsService.Current.RunHistory ?? new List<string>();

        _errorTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(4) };
        _errorTimer.Tick += (_, _) => DismissError();

        ExecuteCommand = new RelayCommand(Execute);
        SelectHistoryItemCommand = new RelayCommand<string>(SelectHistoryItem);
        RemoveHistoryItemCommand = new RelayCommand<string>(RemoveHistoryItem);
        ClearErrorCommand = new RelayCommand(() => DismissError());

        RefreshLabels();
    }

    public void OnLanguageChanged()
    {
        RefreshLabels();
    }

    private void RefreshLabels()
    {
        PlaceholderLabel = _loc.Get("runbox.placeholder");
        OnPropertyChanged(nameof(PlaceholderLabel));
    }

    // ── Arrow key navigation ─────────────────────────────────────────────────

    /// <summary>
    /// Opens the history dropdown with filtered or recent items.
    /// Called by arrow-down or explicit user action. Not called on focus.
    /// </summary>
    public void OpenHistory()
    {
        UpdateFilteredHistory();
        if (FilteredHistory.Count > 0)
        {
            IsHistoryOpen = true;
            SelectedHistoryIndex = 0;
            _runText = FilteredHistory[0];
            OnPropertyChanged(nameof(RunText));
        }
    }

    /// <summary>
    /// Moves selection in the history dropdown. Returns true if handled.
    /// Updates RunText to reflect the selected item without re-triggering the filter.
    /// </summary>
    public bool MoveSelection(int delta)
    {
        if (!_isHistoryOpen)
        {
            OpenHistory();
            return FilteredHistory.Count > 0;
        }

        if (FilteredHistory.Count == 0)
            return false;

        int next = _selectedHistoryIndex + delta;
        if (next < 0) next = FilteredHistory.Count - 1;
        else if (next >= FilteredHistory.Count) next = 0;

        SelectedHistoryIndex = next;
        _runText = FilteredHistory[next];
        OnPropertyChanged(nameof(RunText));
        return true;
    }

    /// <summary>
    /// Commits the currently selected history item to the text box (Tab completion).
    /// </summary>
    public void CommitSelection()
    {
        if (_selectedHistoryIndex >= 0 && _selectedHistoryIndex < FilteredHistory.Count)
        {
            _runText = FilteredHistory[_selectedHistoryIndex];
            OnPropertyChanged(nameof(RunText));
            IsHistoryOpen = false;
            SelectedHistoryIndex = -1;
        }
    }

    /// <summary>
    /// Handles Escape key with two-stage behavior:
    /// First press closes the dropdown, second press collapses the widget.
    /// </summary>
    public void HandleEscape()
    {
        if (_isHistoryOpen)
        {
            IsHistoryOpen = false;
            SelectedHistoryIndex = -1;
            return;
        }

        RequestCollapse();
    }

    public void RequestCollapse()
    {
        IsHistoryOpen = false;
        RunText = string.Empty;
        DismissError();
        CollapseRequested?.Invoke(this, EventArgs.Empty);
    }

    // ── Error toast ──────────────────────────────────────────────────────────

    private void ShowErrorToast(string message)
    {
        ErrorText = message;
        ShowError = true;

        _errorTimer.Stop();
        _errorTimer.Start();
    }

    private void DismissError()
    {
        _errorTimer.Stop();
        ErrorText = string.Empty;
        ShowError = false;
    }

    // ── Execution logic ───────────────────────────────────────────────────

    private void Execute()
    {
        // If the dropdown is open with a highlighted suggestion, run that suggestion
        // instead of whatever the user has partially typed. This lets Enter act on the
        // auto-selected best match while typing. Escape can be used to dismiss the
        // suggestion and run the literal typed text.
        string? input;
        if (_isHistoryOpen
            && _selectedHistoryIndex >= 0
            && _selectedHistoryIndex < FilteredHistory.Count)
        {
            input = FilteredHistory[_selectedHistoryIndex]?.Trim();
        }
        else
        {
            input = _runText?.Trim();
        }

        if (string.IsNullOrEmpty(input))
            return;

        DismissError();
        IsHistoryOpen = false;
        SelectedHistoryIndex = -1;

        bool success = false;

        if (IsFilePath(input))
        {
            success = TryOpenPath(input);
        }
        else if (IsExplicitUrl(input))
        {
            success = TryOpenUrl(input);
        }
        else
        {
            // Try to execute as a shell command / program first (handles .cpl, .msc,
            // executable names, etc.). Only fall back to URL open or search if it fails.
            success = TryShellExecute(input);
            if (!success)
            {
                if (IsUrl(input))
                    success = TryOpenUrl(input);
                else
                    success = TrySearchFallback(input);
            }
        }

        if (success)
        {
            _runText = string.Empty;
            OnPropertyChanged(nameof(RunText));
            CollapseRequested?.Invoke(this, EventArgs.Empty);
        }
    }

    private bool TryOpenUrl(string input)
    {
        var url = input;
        if (!url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
            !url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            url = "https://" + url;

        try
        {
            Process.Start(new ProcessStartInfo { FileName = url, UseShellExecute = true });
            Log.Action($"runbox: opened URL \"{url}\"");
            AddToHistory(input);
            return true;
        }
        catch (Exception ex)
        {
            ShowErrorToast($"{_loc.Get("runbox.error")}: {ex.Message}");
            Log.Error($"runbox: failed to open URL \"{url}\": {ex.Message}");
            return false;
        }
    }

    private bool TryOpenPath(string input)
    {
        try
        {
            var psi = new ProcessStartInfo { FileName = input, UseShellExecute = true };
            // If it's a directory, Explorer opens it. If it's a file, the default app opens it.
            Process.Start(psi);
            Log.Action($"runbox: opened path \"{input}\"");
            AddToHistory(input);
            return true;
        }
        catch (Exception ex)
        {
            ShowErrorToast($"{_loc.Get("runbox.error")}: {ex.Message}");
            Log.Error($"runbox: failed to open path \"{input}\": {ex.Message}");
            return false;
        }
    }

    private bool TryShellExecute(string input)
    {
        var (fileName, arguments) = ParseCommandLine(input);
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                UseShellExecute = true,
            });
            Log.Action($"runbox: shell executed \"{input}\"");
            AddToHistory(input);
            return true;
        }
        catch
        {
            return false; // Caller will try search fallback
        }
    }

    /// <summary>
    /// Splits a raw command-line string into executable and arguments, matching
    /// the same parsing Windows native Run dialog applies. Quoted executables
    /// such as "C:\Program Files\app.exe" --flag are handled correctly.
    /// </summary>
    private static (string fileName, string arguments) ParseCommandLine(string input)
    {
        if (input.StartsWith('"'))
        {
            var close = input.IndexOf('"', 1);
            if (close > 1)
                return (input[1..close].Trim(), input[(close + 1)..].TrimStart());
            // Unclosed quote: strip the leading quote and fall through to space-split.
            input = input[1..].TrimStart();
        }

        var space = input.IndexOf(' ');
        return space > 0
            ? (input[..space], input[(space + 1)..].TrimStart())
            : (input, string.Empty);
    }

    private bool TrySearchFallback(string input)
    {
        try
        {
            // Use the default browser's search by opening a generic search URL.
            // The system default browser handles the actual search engine.
            var encoded = Uri.EscapeDataString(input);
            Process.Start(new ProcessStartInfo
            {
                FileName = $"https://www.google.com/search?q={encoded}",
                UseShellExecute = true
            });
            Log.Action($"runbox: searched \"{input}\"");
            AddToHistory(input);
            return true;
        }
        catch (Exception ex)
        {
            ShowErrorToast($"{_loc.Get("runbox.error")}: {ex.Message}");
            Log.Error($"runbox: failed to search \"{input}\": {ex.Message}");
            return false;
        }
    }

    private void AddToHistory(string entry)
    {
        _history.RemoveAll(h => string.Equals(h, entry, StringComparison.OrdinalIgnoreCase));
        _history.Insert(0, entry);

        if (_history.Count > 50)
            _history.RemoveRange(50, _history.Count - 50);

        _settingsService.Current.RunHistory = new List<string>(_history);
        _settingsService.Save();
    }

    public void RemoveHistoryItem(string? item)
    {
        if (item is null) return;
        _history.RemoveAll(h => string.Equals(h, item, StringComparison.OrdinalIgnoreCase));
        _settingsService.Current.RunHistory = new List<string>(_history);
        _settingsService.Save();

        // Refresh the dropdown (case-insensitive to match _history removal)
        var match = FilteredHistory.FirstOrDefault(h => string.Equals(h, item, StringComparison.OrdinalIgnoreCase));
        if (match is not null)
            FilteredHistory.Remove(match);
        if (FilteredHistory.Count == 0)
            IsHistoryOpen = false;
        else if (_selectedHistoryIndex >= FilteredHistory.Count)
            SelectedHistoryIndex = FilteredHistory.Count - 1;
    }

    private void SelectHistoryItem(string? item)
    {
        if (item is null) return;
        _runText = item;
        OnPropertyChanged(nameof(RunText));
        IsHistoryOpen = false;
        SelectedHistoryIndex = -1;
        Execute();
    }

    private void UpdateFilteredHistory()
    {
        FilteredHistory.Clear();
        if (_history.Count == 0)
        {
            IsHistoryOpen = false;
            return;
        }

        var filter = _runText?.Trim() ?? string.Empty;
        IEnumerable<string> matches;
        if (string.IsNullOrEmpty(filter))
        {
            matches = _history.Take(10);
        }
        else
        {
            // Rank prefix matches above substring matches so the "most matched"
            // entry surfaces first. Within the same rank, preserve MRU order.
            matches = _history
                .Select((h, i) => (h, i, rank:
                    h.StartsWith(filter, StringComparison.OrdinalIgnoreCase) ? 0 :
                    h.Contains(filter, StringComparison.OrdinalIgnoreCase) ? 1 : -1))
                .Where(x => x.rank >= 0)
                .OrderBy(x => x.rank).ThenBy(x => x.i)
                .Select(x => x.h)
                .Take(10);
        }

        foreach (var m in matches)
            FilteredHistory.Add(m);

        // Auto-open while typing when there are matches.
        // Stays closed when filter is empty (opened explicitly via arrow-down).
        if (FilteredHistory.Count == 0)
            IsHistoryOpen = false;
        else if (!string.IsNullOrEmpty(filter))
            IsHistoryOpen = true;
    }

    // ── Input classification ─────────────────────────────────────────────────

    /// <summary>
    /// Returns true only when the input starts with an explicit URL scheme.
    /// Used as the first URL check so that shell commands like appwiz.cpl
    /// are not mistaken for URLs before the shell has had a chance to run.
    /// </summary>
    private static bool IsExplicitUrl(string input) =>
        input.StartsWith("http://",  StringComparison.OrdinalIgnoreCase) ||
        input.StartsWith("https://", StringComparison.OrdinalIgnoreCase) ||
        input.StartsWith("www.", StringComparison.OrdinalIgnoreCase) ||
        input.EndsWith(".com", StringComparison.OrdinalIgnoreCase) ||
        input.EndsWith(".np", StringComparison.OrdinalIgnoreCase) ||
        input.EndsWith(".net", StringComparison.OrdinalIgnoreCase) ||
        input.StartsWith("ftp://",   StringComparison.OrdinalIgnoreCase);

    // Compiled regex for URL detection including domains, IPs, localhost, ports, query strings
    private static readonly Regex UrlPattern = new(
        @"^(" +
            @"https?://\S+" +                                // explicit scheme (must have content after)
            @"|[\w\-]+(\.[\w\-]+)+([:/\?]\S*)?" +           // domain.tld with optional port/path/query
            @"|localhost(:\d+)?(/.*)?" +                     // localhost with optional port/path
            @"|\d{1,3}\.\d{1,3}\.\d{1,3}\.\d{1,3}(:\d+)?(/.*)?" + // IPv4 with optional port/path
        @")$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    internal static bool IsUrl(string input)
    {
        return UrlPattern.IsMatch(input);
    }

    internal static bool IsFilePath(string input)
    {
        // Detect Windows absolute paths (C:\..., D:\...) and UNC paths (\\server\share)
        if (input.Length >= 3 &&
            char.IsLetter(input[0]) && input[1] == ':' && (input[2] == '\\' || input[2] == '/'))
        {
            return Directory.Exists(input) || File.Exists(input);
        }

        if (input.StartsWith(@"\\"))
        {
            return true; // UNC path, let shell handle it
        }

        return false;
    }
}
