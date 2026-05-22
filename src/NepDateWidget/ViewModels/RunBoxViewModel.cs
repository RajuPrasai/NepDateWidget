using NepDateWidget.Helpers;
using NepDateWidget.Models;
using NepDateWidget.Services;
using System.Collections.ObjectModel;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
using System.Windows.Input;
using System.Windows.Threading;

namespace NepDateWidget.ViewModels;

public sealed record HistoryEntry(string Raw, string? Prefix, string Query)
{
    public bool HasPrefix => Prefix is not null;
}

public sealed class RunBoxViewModel : ViewModelBase
{
    private const string ScriptPrefix = "scr";

    private readonly ISearchHistoryService? _runHistoryService;
    private readonly ILocalizationService _loc;
    private readonly IShortcutsService _shortcuts;
    private readonly IScriptService? _scriptService;
    private readonly DispatcherTimer _errorTimer;
    private HashSet<string> _knownPrefixKeys;

    private string _runText = string.Empty;
    public string RunText
    {
        get => _runText;
        set
        {
            if (SetProperty(ref _runText, value))
            {
                // Detect prefix locking: user typed "{prefix} " with no active prefix yet.
                if (_activePrefix is null && value.EndsWith(' '))
                {
                    var candidate = value.TrimEnd();
                    if (candidate.Length > 0 &&
                        (_shortcuts.Prefixes.ContainsKey(candidate) ||
                         (_scriptService is not null && string.Equals(candidate, ScriptPrefix, StringComparison.OrdinalIgnoreCase))))
                    {
                        SetActivePrefix(candidate);
                        return;
                    }
                }

                // Calculator mode: input starts with '=' (only when no prefix is active).
                // When a prefix is locked the text is a search query, not a formula.
                if (_activePrefix is null && value.StartsWith('='))
                {
                    var result = EvaluateExpression(value[1..].Trim());
                    CalcResult = result ?? string.Empty;
                    ShowCalcResult = result is not null;
                    FilteredHistory.Clear();
                    IsHistoryOpen = false;
                    return;
                }

                ShowCalcResult = false;
                CalcResult = string.Empty;

                UpdateFilteredHistory();
                // While the user is typing, auto-highlight the best match so Enter executes it.
                // Clearing the filtered list above resets the ListBox's internal selection,
                // so we must always go through -1 before re-selecting 0; otherwise the
                // OneWay binding sees no change and the highlight is dropped on the next keystroke.
                SelectedHistoryIndex = -1;
                if (_isHistoryOpen && FilteredHistory.Count > 0)
                {
                    SelectedHistoryIndex = 0;
                }
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

    private string? _activePrefix;
    public string? ActivePrefix
    {
        get => _activePrefix;
        private set
        {
            if (SetProperty(ref _activePrefix, value))
            {
                OnPropertyChanged(nameof(HasActivePrefix));
            }
        }
    }

    public bool HasActivePrefix => _activePrefix is not null;

    private string _calcResult = string.Empty;
    public string CalcResult
    {
        get => _calcResult;
        private set => SetProperty(ref _calcResult, value);
    }

    private bool _showCalcResult;
    public bool ShowCalcResult
    {
        get => _showCalcResult;
        private set => SetProperty(ref _showCalcResult, value);
    }

    public ObservableCollection<HistoryEntry> FilteredHistory { get; } = new();

    // Labels
    public string PlaceholderLabel { get; private set; } = string.Empty;
    public string HotkeyHintLabel { get; private set; } = string.Empty;
    public string SearchHintLabel { get; private set; } = string.Empty;

    // Commands
    public ICommand ExecuteCommand { get; }
    public ICommand SelectHistoryItemCommand { get; }
    public ICommand RemoveHistoryItemCommand { get; }
    public ICommand ClearErrorCommand { get; }

    /// <summary>
    /// Raised when the RunBox requests the widget to collapse (Escape key).
    /// </summary>
    public event EventHandler? CollapseRequested;
    public event EventHandler? ExecutedSuccessfully;

    public RunBoxViewModel(ISearchHistoryService? runHistoryService, ILocalizationService localizationService, IShortcutsService shortcutsService, IScriptService? scriptService = null)
    {
        _runHistoryService = runHistoryService;
        _loc = localizationService;
        _shortcuts = shortcutsService;
        _scriptService = scriptService;
        _knownPrefixKeys = new HashSet<string>(_shortcuts.Prefixes.Keys, StringComparer.OrdinalIgnoreCase);

        _errorTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(4) };
        _errorTimer.Tick += (_, _) => DismissError();

        _shortcuts.ShortcutsChanged += OnShortcutsChanged;
        _scriptService?.ScriptsChanged += OnScriptsChanged;

        ExecuteCommand = new RelayCommand(Execute);
        SelectHistoryItemCommand = new RelayCommand<HistoryEntry>(SelectHistoryItem);
        RemoveHistoryItemCommand = new RelayCommand<HistoryEntry>(RemoveHistoryItem);
        ClearErrorCommand = new RelayCommand(() => DismissError());

        RefreshLabels();
    }

    public void OnLanguageChanged()
    {
        RefreshLabels();
    }

    /// <summary>
    /// Called by MainWindow after the hotkey is registered so the strip/spotlight
    /// can display the actual bound key combination.
    /// </summary>
    public void SetHotkeyHint(string label)
    {
        HotkeyHintLabel = label;
        OnPropertyChanged(nameof(HotkeyHintLabel));
    }

    /// <summary>
    /// Populates FilteredHistory from recent history without modifying RunText or
    /// selection. Called by the spotlight on first show so the list is pre-loaded.
    /// </summary>
    public void LoadInitialHistory() => UpdateFilteredHistory();

    private void RefreshLabels()
    {
        PlaceholderLabel = _loc.Get("runbox.placeholder");
        OnPropertyChanged(nameof(PlaceholderLabel));
        if (_activePrefix is null)
        {
            SearchHintLabel = PlaceholderLabel;
            OnPropertyChanged(nameof(SearchHintLabel));
        }
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
            var first = FilteredHistory[0];
            _runText = _activePrefix is not null ? first.Query : first.Raw;
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
        {
            return false;
        }

        int next = _selectedHistoryIndex + delta;
        if (next < 0)
        {
            next = FilteredHistory.Count - 1;
        }
        else if (next >= FilteredHistory.Count)
        {
            next = 0;
        }

        SelectedHistoryIndex = next;
        var entry = FilteredHistory[next];
        _runText = _activePrefix is not null ? entry.Query : entry.Raw;
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
            var entry = FilteredHistory[_selectedHistoryIndex];
            if (entry.HasPrefix)
            {
                // Lock the prefix pill and place the query in the text box.
                // Set _runText directly then call UpdateFilteredHistory once
                // instead of going through SetActivePrefix (which resets to empty)
                // then RunText = entry.Query (which filters again).
                ActivePrefix = entry.Prefix!;
                ApplyPrefixHintLabel(entry.Prefix!);
                ShowCalcResult = false;
                CalcResult = string.Empty;
                IsHistoryOpen = false;
                SelectedHistoryIndex = -1;
                _runText = entry.Query;
                OnPropertyChanged(nameof(RunText));
                UpdateFilteredHistory();
            }
            else
            {
                _runText = entry.Raw;
                OnPropertyChanged(nameof(RunText));
                IsHistoryOpen = false;
                SelectedHistoryIndex = -1;
            }
        }
    }

    // Overload used by CommitSelection to set hint label consistently for all prefix types.
    private void ApplyPrefixHintLabel(string prefix)
    {
        if (string.Equals(prefix, ScriptPrefix, StringComparison.OrdinalIgnoreCase))
        {
            SearchHintLabel = _loc.Get("runbox.script_search_hint");
        }
        else
        {
            string site = _shortcuts.PrefixSiteNames.TryGetValue(prefix, out var n) ? n : prefix;
            SearchHintLabel = $"Search {site}...";
        }
        OnPropertyChanged(nameof(SearchHintLabel));
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

        if (_showCalcResult)
        {
            RunText = string.Empty;
            return;
        }

        if (_activePrefix is not null)
        {
            ClearPrefix();
            return;
        }

        RequestCollapse();
    }

    public void RequestCollapse()
    {
        IsHistoryOpen = false;
        // Reset prefix state directly - avoids UpdateFilteredHistory running
        // inside ClearPrefix, since we're closing and the list is irrelevant.
        if (_activePrefix is not null)
        {
            ActivePrefix = null;
            SearchHintLabel = PlaceholderLabel;
            OnPropertyChanged(nameof(SearchHintLabel));
        }
        // Reset RunText directly - avoids UpdateFilteredHistory running through
        // the setter, since we're closing and the list is irrelevant.
        _runText = string.Empty;
        OnPropertyChanged(nameof(RunText));
        ShowCalcResult = false;
        CalcResult = string.Empty;
        FilteredHistory.Clear();
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
        // "help" command: open the RunBox help window.
        var rawInput = _runText?.Trim();
        if (string.Equals(rawInput, "help", StringComparison.OrdinalIgnoreCase)
            && _activePrefix is null)
        {
            var spotlight = System.Windows.Application.Current.Windows
                .OfType<NepDateWidget.Views.RunBoxSpotlightWindow>()
                .FirstOrDefault(w => w.IsVisible);
            var shell = System.Windows.Application.Current.Windows
                .OfType<NepDateWidget.Views.ExpandedShellWindow>()
                .FirstOrDefault(w => w.IsVisible)
                ?? (System.Windows.Window)System.Windows.Application.Current.MainWindow!;
            var win = new NepDateWidget.Views.RunBoxHelpWindow(_loc, _shortcuts, _scriptService)
            {
                Owner = (System.Windows.Window?)spotlight ?? shell,
                WindowStartupLocation = System.Windows.WindowStartupLocation.CenterOwner
            };
            win.Show();
            win.Activate();
            RunText = string.Empty;
            ExecutedSuccessfully?.Invoke(this, EventArgs.Empty);
            return;
        }

        // Calculator mode: copy result to clipboard and close.
        // Script results use the same ShowCalcResult flag for display, so exclude them.
        if (_showCalcResult && !string.IsNullOrEmpty(_calcResult)
            && !string.Equals(_activePrefix, ScriptPrefix, StringComparison.OrdinalIgnoreCase))
        {
            TryCopyToClipboard(_calcResult);
            ShowCalcResult = false;
            CalcResult = string.Empty;
            DismissError();
            ExecutedSuccessfully?.Invoke(this, EventArgs.Empty);
            return;
        }

        // If the dropdown is open and an item is highlighted, execute it directly.
        // This is the uniform path for all modes: scr prefix, shortcut prefix, and plain history.
        // The per-mode blocks below only run when the user presses Enter on their own typed text
        // with no highlighted suggestion.
        if (_isHistoryOpen && _selectedHistoryIndex >= 0 && _selectedHistoryIndex < FilteredHistory.Count)
        {
            SelectHistoryItem(FilteredHistory[_selectedHistoryIndex]);
            return;
        }

        // Script execution mode
        if (string.Equals(_activePrefix, ScriptPrefix, StringComparison.OrdinalIgnoreCase))
        {
            var scriptInput = _runText?.Trim() ?? string.Empty;
            if (string.IsNullOrEmpty(scriptInput))
            {
                return;
            }

            // First token is the script name; remainder are arguments.
            var scriptSpaceAt = scriptInput.IndexOf(' ');
            var scriptName = scriptSpaceAt > 0 ? scriptInput[..scriptSpaceAt].Trim() : scriptInput;
            var scriptArgs = scriptSpaceAt > 0 ? scriptInput[(scriptSpaceAt + 1)..].Trim() : string.Empty;

            var script = _scriptService?.Find(scriptName);
            if (script is null)
            {
                ShowErrorToast(string.Format(_loc.Get("runbox.script_not_found"), scriptName));
                return;
            }

            DismissError();
            if (LaunchScript(script, scriptArgs))
            {
                ClearPrefix();
                ExecutedSuccessfully?.Invoke(this, EventArgs.Empty);
            }
            return;
        }

        // Prefix search mode (shortcuts → URL)
        if (_activePrefix is not null)
        {
            var query = _runText?.Trim() ?? string.Empty;
            if (string.IsNullOrEmpty(query))
            {
                return;
            }

            DismissError();
            string url = BuildPrefixUrl(_activePrefix, Uri.EscapeDataString(query));
            try
            {
                Process.Start(new ProcessStartInfo { FileName = url, UseShellExecute = true });
                Log.Action($"runbox: prefix search [{_activePrefix}] \"{query}\"");
                AddToHistory($"{_activePrefix} {query}");
                _runText = string.Empty;
                OnPropertyChanged(nameof(RunText));
                ClearPrefix();
                ExecutedSuccessfully?.Invoke(this, EventArgs.Empty);
            }
            catch (Exception ex)
            {
                ShowErrorToast($"{_loc.Get("runbox.error")}: {ex.Message}");
                Log.Error($"runbox: prefix search failed [{_activePrefix}] \"{query}\": {ex.Message}");
            }
            return;
        }

        // No prefix: run the typed text directly through the detection pipeline.
        var input = _runText?.Trim();

        if (string.IsNullOrEmpty(input))
        {
            return;
        }

        DismissError();
        IsHistoryOpen = false;
        SelectedHistoryIndex = -1;

        // Re-execute a stored prefix search from history (e.g., "yt cats", "maps Kathmandu").
        int spaceAt = input.IndexOf(' ');
        if (spaceAt > 0)
        {
            string potentialPrefix = input[..spaceAt];
            string potentialQuery = input[(spaceAt + 1)..].Trim();
            if (potentialQuery.Length > 0 && _shortcuts.Prefixes.ContainsKey(potentialPrefix))
            {
                string url = BuildPrefixUrl(potentialPrefix, Uri.EscapeDataString(potentialQuery));
                try
                {
                    Process.Start(new ProcessStartInfo { FileName = url, UseShellExecute = true });
                    Log.Action($"runbox: prefix re-run [{potentialPrefix}] \"{potentialQuery}\"");
                    AddToHistory(input);
                    _runText = string.Empty;
                    OnPropertyChanged(nameof(RunText));
                    ExecutedSuccessfully?.Invoke(this, EventArgs.Empty);
                }
                catch (Exception ex)
                {
                    ShowErrorToast($"{_loc.Get("runbox.error")}: {ex.Message}");
                    Log.Error($"runbox: prefix re-run failed [{potentialPrefix}] \"{potentialQuery}\": {ex.Message}");
                }
                return;
            }
        }

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
                {
                    success = TryOpenUrl(input);
                }
                else
                {
                    success = TrySearchFallback(input);
                }
            }
        }

        if (success)
        {
            _runText = string.Empty;
            OnPropertyChanged(nameof(RunText));
            ExecutedSuccessfully?.Invoke(this, EventArgs.Empty);
        }
    }

    private bool TryOpenUrl(string input)
    {
        var url = input;
        if (!url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
            !url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            url = "https://" + url;
        }

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
            {
                return (input[1..close].Trim(), input[(close + 1)..].TrimStart());
            }
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
        _runHistoryService?.Record(entry);
    }

    public void RemoveHistoryItem(HistoryEntry? item)
    {
        if (item is null)
        {
            return;
        }
        // Script entries exist only in the registry; they are never in run history.
        // Removing them from the visible list is meaningless - they reappear on the
        // next filter update. Treat as a no-op so the UX is not misleading.
        if (string.Equals(item.Prefix, ScriptPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        _runHistoryService?.Remove(item.Raw);

        var match = FilteredHistory.FirstOrDefault(h => string.Equals(h.Raw, item.Raw, StringComparison.OrdinalIgnoreCase));
        if (match is not null)
        {
            FilteredHistory.Remove(match);
        }

        if (FilteredHistory.Count == 0)
        {
            IsHistoryOpen = false;
        }
        else if (_selectedHistoryIndex >= FilteredHistory.Count)
        {
            SelectedHistoryIndex = FilteredHistory.Count - 1;
        }
    }

    private void SelectHistoryItem(HistoryEntry? item)
    {
        if (item is null)
        {
            return;
        }

        IsHistoryOpen = false;
        SelectedHistoryIndex = -1;

        if (item.HasPrefix)
        {
            // Script entries: execute immediately without building a URL.
            if (string.Equals(item.Prefix, ScriptPrefix, StringComparison.OrdinalIgnoreCase))
            {
                DismissError();
                var script = _scriptService?.Find(item.Query);
                if (script is null)
                {
                    ShowErrorToast(string.Format(_loc.Get("runbox.script_not_found"), item.Query));
                    return;
                }
                if (LaunchScript(script, string.Empty))
                {
                    ExecutedSuccessfully?.Invoke(this, EventArgs.Empty);
                }

                return;
            }

            DismissError();
            string url = BuildPrefixUrl(item.Prefix!, Uri.EscapeDataString(item.Query));
            try
            {
                Process.Start(new ProcessStartInfo { FileName = url, UseShellExecute = true });
                Log.Action($"runbox: prefix re-run [{item.Prefix}] \"{item.Query}\"");
                AddToHistory(item.Raw);
                _runText = string.Empty;
                OnPropertyChanged(nameof(RunText));
                ClearPrefix();
                ExecutedSuccessfully?.Invoke(this, EventArgs.Empty);
            }
            catch (Exception ex)
            {
                ShowErrorToast($"{_loc.Get("runbox.error")}: {ex.Message}");
                Log.Error($"runbox: prefix re-run failed [{item.Prefix}] \"{item.Query}\": {ex.Message}");
            }
            return;
        }

        _runText = item.Raw;
        OnPropertyChanged(nameof(RunText));
        Execute();
    }

    private IReadOnlyList<string> GetHistory() =>
        _runHistoryService?.GetMatching("", 5000) ?? Array.Empty<string>();

    private void UpdateFilteredHistory()
    {
        FilteredHistory.Clear();

        // Script prefix: populated from the registry, never from run history.
        // Must be checked before the history.Count == 0 early-exit guard so that
        // the script list is shown even when run history is empty (fresh install).
        if (string.Equals(_activePrefix, ScriptPrefix, StringComparison.OrdinalIgnoreCase))
        {
            var scriptFilter = _runText?.Trim() ?? string.Empty;
            var scripts = _scriptService?.GetAll() ?? (IReadOnlyList<ScriptEntry>)Array.Empty<ScriptEntry>();

            IEnumerable<ScriptEntry> ranked;
            if (string.IsNullOrEmpty(scriptFilter))
            {
                ranked = scripts;
            }
            else
            {
                ranked = scripts
                    .Select((s, i) => (s, i, rank:
                        s.Name.StartsWith(scriptFilter, StringComparison.OrdinalIgnoreCase) ? 0 :
                        s.Name.Contains(scriptFilter, StringComparison.OrdinalIgnoreCase) ? 1 :
                        s.Description.Contains(scriptFilter, StringComparison.OrdinalIgnoreCase) ? 2 : -1))
                    .Where(x => x.rank >= 0)
                    .OrderBy(x => x.rank).ThenBy(x => x.i)
                    .Select(x => x.s);
            }

            foreach (var s in ranked.Take(10))
            {
                FilteredHistory.Add(new HistoryEntry($"{ScriptPrefix} {s.Name}", ScriptPrefix, s.Name));
            }

            IsHistoryOpen = FilteredHistory.Count > 0;
            return;
        }

        var history = GetHistory();
        if (history.Count == 0)
        {
            IsHistoryOpen = false;
            return;
        }

        var filter = _runText?.Trim() ?? string.Empty;

        if (_activePrefix is not null)
        {
            // Prefix active: show only history for this prefix, matched against the query portion.
            // Parse each entry once to avoid the double-parse of Where+Select.
            var prefixMatches = history
                .Select(h => ParseHistoryEntry(h))
                .Where(e => e.HasPrefix && string.Equals(e.Prefix, _activePrefix, StringComparison.OrdinalIgnoreCase))
                .Select((e, i) =>
                {
                    int rank = string.IsNullOrEmpty(filter) ? 0 :
                        e.Query.StartsWith(filter, StringComparison.OrdinalIgnoreCase) ? 0 :
                        e.Query.Contains(filter, StringComparison.OrdinalIgnoreCase) ? 1 : -1;
                    return (e, i, rank);
                })
                .Where(x => string.IsNullOrEmpty(filter) || x.rank >= 0)
                .OrderBy(x => x.rank).ThenBy(x => x.i)
                .Select(x => x.e)
                .Take(10);

            foreach (var m in prefixMatches)
            {
                FilteredHistory.Add(m);
            }

            IsHistoryOpen = FilteredHistory.Count > 0;
            return;
        }

        // No prefix: parse once and filter to plain (non-prefix) items only.
        var plainPool = history
            .Select(h => ParseHistoryEntry(h))
            .Where(e => !e.HasPrefix);

        IEnumerable<HistoryEntry> matches;
        if (string.IsNullOrEmpty(filter))
        {
            matches = plainPool.Take(10);
        }
        else
        {
            matches = plainPool
                .Select((e, i) => (e, i, rank:
                    e.Raw.StartsWith(filter, StringComparison.OrdinalIgnoreCase) ? 0 :
                    e.Raw.Contains(filter, StringComparison.OrdinalIgnoreCase) ? 1 : -1))
                .Where(x => x.rank >= 0)
                .OrderBy(x => x.rank).ThenBy(x => x.i)
                .Select(x => x.e)
                .Take(10);
        }

        foreach (var m in matches)
        {
            FilteredHistory.Add(m);
        }

        // Auto-open while typing when there are matches.
        // Stays closed when filter is empty (opened explicitly via arrow-down).
        if (FilteredHistory.Count == 0)
        {
            IsHistoryOpen = false;
        }
        else if (!string.IsNullOrEmpty(filter))
        {
            IsHistoryOpen = true;
        }
    }

    // ── Input classification ─────────────────────────────────────────────────

    /// <summary>
    /// Returns true only when the input starts with an explicit URL scheme.
    /// Used as the first URL check so that shell commands like appwiz.cpl
    /// are not mistaken for URLs before the shell has had a chance to run.
    /// </summary>
    private static bool IsExplicitUrl(string input) =>
        input.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
        input.StartsWith("https://", StringComparison.OrdinalIgnoreCase) ||
        input.StartsWith("www.", StringComparison.OrdinalIgnoreCase) ||
        input.EndsWith(".com", StringComparison.OrdinalIgnoreCase) ||
        input.EndsWith(".np", StringComparison.OrdinalIgnoreCase) ||
        input.EndsWith(".net", StringComparison.OrdinalIgnoreCase) ||
        input.StartsWith("ftp://", StringComparison.OrdinalIgnoreCase);

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

    // ── Prefix + Calculator ──────────────────────────────────────────────────

    private void SetActivePrefix(string prefix)
    {
        ActivePrefix = prefix;
        _runText = string.Empty;
        OnPropertyChanged(nameof(RunText));
        ShowCalcResult = false;
        CalcResult = string.Empty;
        FilteredHistory.Clear();
        IsHistoryOpen = false;
        SelectedHistoryIndex = -1;
        ApplyPrefixHintLabel(prefix);
        UpdateFilteredHistory();
    }

    public void ClearPrefix()
    {
        if (_activePrefix is null)
        {
            return;
        }

        ActivePrefix = null;
        SearchHintLabel = PlaceholderLabel;
        OnPropertyChanged(nameof(SearchHintLabel));
        UpdateFilteredHistory();
    }

    private string BuildPrefixUrl(string prefix, string encodedQuery) =>
        string.Format(
            _shortcuts.Prefixes[prefix].Replace("{year}", DateTime.Now.Year.ToString()),
            encodedQuery);

    private HistoryEntry ParseHistoryEntry(string raw)
    {
        int space = raw.IndexOf(' ');
        if (space > 0)
        {
            string prefix = raw[..space];
            string query = raw[(space + 1)..].Trim();
            if (query.Length > 0 && _shortcuts.Prefixes.ContainsKey(prefix))
            {
                return new HistoryEntry(raw, prefix, query);
            }
        }
        return new HistoryEntry(raw, null, raw);
    }

    private void OnShortcutsChanged(object? sender, EventArgs e)
    {
        var newKeys = new HashSet<string>(_shortcuts.Prefixes.Keys, StringComparer.OrdinalIgnoreCase);
        var removedKeys = _knownPrefixKeys
            .Where(k => !newKeys.Contains(k))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        _knownPrefixKeys = newKeys;

        if (removedKeys.Count > 0)
        {
            // Purge history entries whose prefix was removed.
            var purgeList = GetHistory()
                .Where(h => { var e = ParseHistoryEntry(h); return e.HasPrefix && removedKeys.Contains(e.Prefix!); })
                .ToList();

            foreach (var h in purgeList)
            {
                _runHistoryService?.Remove(h);
            }

            if (purgeList.Count > 0 && _activePrefix is not null && removedKeys.Contains(_activePrefix))
            {
                // Clear the active prefix directly - ClearPrefix() calls UpdateFilteredHistory(),
                // so we return immediately to avoid the redundant call below.
                ClearPrefix();
                return;
            }
        }

        UpdateFilteredHistory();
    }

    private void OnScriptsChanged(object? sender, EventArgs e)
    {
        if (string.Equals(_activePrefix, ScriptPrefix, StringComparison.OrdinalIgnoreCase))
        {
            UpdateFilteredHistory();
        }
    }

    private static string? EvaluateExpression(string expr)
    {
        if (string.IsNullOrWhiteSpace(expr))
        {
            return null;
        }

        try
        {
            var result = new DataTable().Compute(expr, null);
            double val = Convert.ToDouble(result);
            return val == Math.Floor(val) && !double.IsInfinity(val) && !double.IsNaN(val)
                ? ((long)val).ToString()
                : val.ToString("G10");
        }
        catch
        {
            return null;
        }
    }

    // Opens the script in a real interactive terminal window so the user can interact freely.
    // The RunBox closes immediately on launch; all I/O happens in the spawned window.
    // Returns true on successful launch, false on validation or launch error.
    private bool LaunchScript(ScriptEntry script, string args)
    {
        string resolvedPath;
        try
        {
            var rawPath = script.Path.Replace("{APPDIR}", AppPaths.ExeDirectory, StringComparison.OrdinalIgnoreCase);
            resolvedPath = Path.GetFullPath(rawPath);
        }
        catch
        {
            ShowErrorToast(string.Format(_loc.Get("runbox.script_not_found"), script.Name));
            return false;
        }
        if (!Path.IsPathRooted(resolvedPath) || !File.Exists(resolvedPath))
        {
            ShowErrorToast(string.Format(_loc.Get("runbox.script_not_found"), script.Name));
            return false;
        }

        var (fileName, arguments) = BuildInterpreterCommand(script, resolvedPath, args);
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                UseShellExecute = true,
                // Run in the script's own directory so relative paths inside scripts resolve correctly.
                WorkingDirectory = Path.GetDirectoryName(resolvedPath) ?? string.Empty,
            });
            Log.Action($"runbox: script [{script.Name}] launched");
            return true;
        }
        catch (Exception ex)
        {
            ShowErrorToast(ex.Message);
            Log.Error($"runbox: script [{script.Name}] launch failed: {ex.Message}");
            return false;
        }
    }

    private static (string fileName, string arguments) BuildInterpreterCommand(ScriptEntry script, string resolvedPath, string args)
    {
        var quotedPath = $"\"{resolvedPath}\"";
        var fullArgs = string.IsNullOrEmpty(args) ? quotedPath : $"{quotedPath} {args}";

        if (script.Interpreter.Equals("wsl", StringComparison.OrdinalIgnoreCase))
        {
            // bash inside WSL only understands POSIX paths; convert C:\foo\bar.sh → /mnt/c/foo/bar.sh.
            // After the script runs, `exec bash` keeps the terminal open so the user can inspect output.
            var wslPath = ToWslPath(resolvedPath);
            // Escape single quotes so the path is safe inside a bash single-quoted string.
            // POSIX pattern: replace each ' with '\'' (end quote, literal backslash-quote, reopen quote).
            var escapedWslPath = wslPath.Replace("'", @"'\''");
            var bashCmd = string.IsNullOrEmpty(args)
                ? $"bash '{escapedWslPath}'; exec bash"
                : $"bash '{escapedWslPath}' {args}; exec bash";
            return ("wsl.exe", $"-- bash -c \"{bashCmd}\"");
        }

        return script.Interpreter.ToLowerInvariant() switch
        {
            "cmd" => ("cmd.exe", $"/k {fullArgs}"),
            "python" => ("cmd.exe", $"/k python.exe {fullArgs}"),
            "pwsh" => ("pwsh.exe", $"-NoExit -ExecutionPolicy Bypass -File {fullArgs}"),
            _ => ("powershell.exe", $"-NoExit -ExecutionPolicy Bypass -File {fullArgs}"),
        };
    }

    // Converts a Windows absolute path to its WSL mount equivalent.
    // C:\foo\bar.sh → /mnt/c/foo/bar.sh
    // Paths that are already POSIX-style are returned with backslashes normalised.
    private static string ToWslPath(string windowsPath)
    {
        if (windowsPath.Length >= 2 && char.IsLetter(windowsPath[0]) && windowsPath[1] == ':')
        {
            return $"/mnt/{char.ToLower(windowsPath[0])}{windowsPath[2..].Replace('\\', '/')}";
        }

        return windowsPath.Replace('\\', '/');
    }

    private static void TryCopyToClipboard(string text)
    {
        try { System.Windows.Clipboard.SetText(text); }
        catch (Exception ex) { Log.Error($"clipboard set failed: {ex.Message}"); }
    }
}
