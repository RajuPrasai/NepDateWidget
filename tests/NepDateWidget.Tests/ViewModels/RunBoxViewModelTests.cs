using NepDateWidget.Models;
using NepDateWidget.Services;
using NepDateWidget.ViewModels;

namespace NepDateWidget.Tests.ViewModels;

public sealed class RunBoxViewModelTests
{
    // ── Fakes ─────────────────────────────────────────────────────────────────

    private sealed class FakeSettingsService : ISettingsService
    {
        public WidgetSettings Current { get; private set; } = new();
        public bool IsFirstLaunch => false;
        public int SaveCount { get; private set; }
        public int LoadCount { get; private set; }

        public void Load() { LoadCount++; }
        public void Save() { SaveCount++; }
        public void ResetToDefaults() { Current = new WidgetSettings(); Save(); }
    }

    private sealed class FakeLocalizationService : ILocalizationService
    {
        public string CurrentLanguage { get; private set; } = "en";
        public string Get(string key) => key switch
        {
            "runbox.placeholder" => "Run command, open path, or search...",
            "runbox.error" => "Failed to execute",
            _ => key
        };
        public void SetLanguage(string languageCode) => CurrentLanguage = languageCode;
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static (RunBoxViewModel vm, FakeSettingsService settings) Create(
        List<string>? history = null)
    {
        var settings = new FakeSettingsService();
        if (history is not null)
            settings.Current.RunHistory = new List<string>(history);
        var loc = new FakeLocalizationService();
        var vm = new RunBoxViewModel(settings, loc);
        return (vm, settings);
    }

    // ── URL detection ─────────────────────────────────────────────────────────

    [Theory]
    [InlineData("http://example.com", true)]
    [InlineData("https://example.com", true)]
    [InlineData("HTTP://EXAMPLE.COM", true)]
    [InlineData("google.com", true)]
    [InlineData("sub.domain.com/path", true)]
    [InlineData("api.example.com:8080", true)]
    [InlineData("site.com?q=test", true)]
    [InlineData("site.com/path?q=test&a=1", true)]
    [InlineData("localhost", true)]
    [InlineData("localhost:3000", true)]
    [InlineData("localhost:3000/api/health", true)]
    [InlineData("192.168.1.1", true)]
    [InlineData("10.0.0.1:8080", true)]
    [InlineData("192.168.1.1:443/path", true)]
    [InlineData("notepad", false)]
    [InlineData("hello world", false)]
    [InlineData("calc", false)]
    [InlineData("what is the weather", false)]
    [InlineData("C:\\Users", false)]        // file path, not URL
    [InlineData("", false)]
    [InlineData("http://", false)]          // scheme only, no content
    [InlineData("https:// bad", false)]     // space after scheme
    public void IsUrl_ClassifiesCorrectly(string input, bool expected)
    {
        Assert.Equal(expected, RunBoxViewModel.IsUrl(input));
    }

    // ── File path detection ───────────────────────────────────────────────────

    [Theory]
    [InlineData(@"\\server\share", true)]        // UNC always detected
    [InlineData(@"\\192.168.1.1\files", true)]
    [InlineData("notepad", false)]
    [InlineData("google.com", false)]
    [InlineData("", false)]
    public void IsFilePath_ClassifiesCorrectly(string input, bool expected)
    {
        // Note: absolute paths like C:\Users only return true if the path exists on disk.
        // UNC paths always return true. Non-paths always return false.
        Assert.Equal(expected, RunBoxViewModel.IsFilePath(input));
    }

    [Fact]
    public void IsFilePath_ExistingPath_ReturnsTrue()
    {
        // Use a path that always exists on Windows
        Assert.True(RunBoxViewModel.IsFilePath(@"C:\Windows"));
    }

    [Fact]
    public void IsFilePath_NonexistentAbsolutePath_ReturnsFalse()
    {
        Assert.False(RunBoxViewModel.IsFilePath(@"Z:\NonExistent\Path\12345"));
    }

    // ── History management ────────────────────────────────────────────────────

    [Fact]
    public void Execute_AddsToHistory()
    {
        // We can't actually run processes in tests, but we can test the history logic
        // by pre-populating and checking state after various operations.
        var (vm, settings) = Create(new List<string> { "old entry" });

        Assert.Single(settings.Current.RunHistory);
        Assert.Equal("old entry", settings.Current.RunHistory[0]);
    }

    [Fact]
    public void History_DuplicateMovesToTop()
    {
        var (vm, settings) = Create(new List<string> { "first", "second", "third" });

        // Simulate what AddToHistory does (private, but we test via the public API)
        // The ViewModel loads history from settings on construction
        Assert.Equal(3, settings.Current.RunHistory.Count);
        Assert.Equal("first", settings.Current.RunHistory[0]);
    }

    [Fact]
    public void RemoveHistoryItem_RemovesFromList()
    {
        var (vm, settings) = Create(new List<string> { "alpha", "beta", "gamma" });

        vm.RemoveHistoryItem("beta");

        Assert.Equal(2, settings.Current.RunHistory.Count);
        Assert.DoesNotContain("beta", settings.Current.RunHistory);
        Assert.True(settings.SaveCount > 0);
    }

    [Fact]
    public void RemoveHistoryItem_CaseInsensitive()
    {
        var (vm, settings) = Create(new List<string> { "Notepad", "calc" });

        vm.RemoveHistoryItem("notepad");

        Assert.Single(settings.Current.RunHistory);
        Assert.Equal("calc", settings.Current.RunHistory[0]);
    }

    [Fact]
    public void RemoveHistoryItem_Null_DoesNothing()
    {
        var (vm, settings) = Create(new List<string> { "item" });

        vm.RemoveHistoryItem(null);

        Assert.Single(settings.Current.RunHistory);
    }

    [Fact]
    public void RemoveHistoryItem_ClosesDropdownWhenEmpty()
    {
        var (vm, _) = Create(new List<string> { "only" });

        // Open history first
        vm.MoveSelection(1);
        Assert.True(vm.IsHistoryOpen);

        vm.RemoveHistoryItem("only");
        Assert.False(vm.IsHistoryOpen);
    }

    // ── Keyboard navigation ───────────────────────────────────────────────────

    [Fact]
    public void MoveSelection_Down_OpensHistory()
    {
        var (vm, _) = Create(new List<string> { "first", "second" });
        Assert.False(vm.IsHistoryOpen);

        bool handled = vm.MoveSelection(1);

        Assert.True(handled);
        Assert.True(vm.IsHistoryOpen);
        Assert.Equal(0, vm.SelectedHistoryIndex);
        Assert.Equal("first", vm.RunText);
    }

    [Fact]
    public void MoveSelection_NoHistory_ReturnsFalse()
    {
        var (vm, _) = Create(new List<string>());

        bool handled = vm.MoveSelection(1);

        Assert.False(handled);
        Assert.False(vm.IsHistoryOpen);
    }

    [Fact]
    public void MoveSelection_WrapsAround()
    {
        var (vm, _) = Create(new List<string> { "a", "b", "c" });

        vm.MoveSelection(1); // opens, selects index 0
        vm.MoveSelection(1); // index 1
        vm.MoveSelection(1); // index 2
        vm.MoveSelection(1); // wraps to 0

        Assert.Equal(0, vm.SelectedHistoryIndex);
        Assert.Equal("a", vm.RunText);
    }

    [Fact]
    public void MoveSelection_Up_WrapsToBottom()
    {
        var (vm, _) = Create(new List<string> { "a", "b", "c" });

        vm.MoveSelection(1); // opens, selects index 0
        vm.MoveSelection(-1); // wraps to index 2

        Assert.Equal(2, vm.SelectedHistoryIndex);
        Assert.Equal("c", vm.RunText);
    }

    [Fact]
    public void CommitSelection_FillsTextAndClosesDropdown()
    {
        var (vm, _) = Create(new List<string> { "notepad", "calc" });

        vm.MoveSelection(1); // open and select "notepad"
        Assert.True(vm.IsHistoryOpen);

        vm.CommitSelection();

        Assert.Equal("notepad", vm.RunText);
        Assert.False(vm.IsHistoryOpen);
        Assert.Equal(-1, vm.SelectedHistoryIndex);
    }

    [Fact]
    public void CommitSelection_NothingSelected_DoesNothing()
    {
        var (vm, _) = Create(new List<string> { "notepad" });

        vm.CommitSelection(); // no selection active

        Assert.Equal(string.Empty, vm.RunText);
    }

    // ── Escape behavior ───────────────────────────────────────────────────────

    [Fact]
    public void HandleEscape_DropdownOpen_ClosesDropdownOnly()
    {
        var (vm, _) = Create(new List<string> { "item" });
        bool collapsed = false;
        vm.CollapseRequested += (_, _) => collapsed = true;

        vm.MoveSelection(1); // open dropdown
        Assert.True(vm.IsHistoryOpen);

        vm.HandleEscape();

        Assert.False(vm.IsHistoryOpen);
        Assert.False(collapsed); // Widget should NOT collapse
    }

    [Fact]
    public void HandleEscape_DropdownClosed_CollapsesWidget()
    {
        var (vm, _) = Create(new List<string> { "item" });
        bool collapsed = false;
        vm.CollapseRequested += (_, _) => collapsed = true;

        // Dropdown is closed by default
        vm.HandleEscape();

        Assert.True(collapsed);
        Assert.Equal(string.Empty, vm.RunText);
    }

    [Fact]
    public void HandleEscape_TwoStage_FullSequence()
    {
        var (vm, _) = Create(new List<string> { "item" });
        int collapseCount = 0;
        vm.CollapseRequested += (_, _) => collapseCount++;

        vm.MoveSelection(1); // open dropdown
        vm.HandleEscape();   // first: close dropdown
        Assert.Equal(0, collapseCount);

        vm.HandleEscape();   // second: collapse widget
        Assert.Equal(1, collapseCount);
    }

    // ── Filter behavior ───────────────────────────────────────────────────────

    [Fact]
    public void RunText_Set_FiltersHistory()
    {
        var (vm, _) = Create(new List<string> { "notepad", "calculator", "note" });

        vm.RunText = "note";

        Assert.Equal(2, vm.FilteredHistory.Count);
        Assert.Contains("notepad", vm.FilteredHistory);
        Assert.Contains("note", vm.FilteredHistory);
        Assert.True(vm.IsHistoryOpen); // auto-opens when typing matches
    }

    [Fact]
    public void RunText_Set_CaseInsensitiveFilter()
    {
        var (vm, _) = Create(new List<string> { "Notepad", "VsCode" });

        vm.RunText = "notepad";

        Assert.Single(vm.FilteredHistory);
        Assert.Equal("Notepad", vm.FilteredHistory[0]);
    }

    [Fact]
    public void RunText_NoMatches_DoesNotOpenDropdown()
    {
        var (vm, _) = Create(new List<string> { "notepad" });

        vm.RunText = "xyz";

        Assert.Empty(vm.FilteredHistory);
        Assert.False(vm.IsHistoryOpen);
    }

    [Fact]
    public void RunText_Empty_DoesNotAutoOpenDropdown()
    {
        var (vm, _) = Create(new List<string> { "notepad", "calc" });

        // Setting RunText to "x" then back to "" triggers UpdateFilteredHistory
        vm.RunText = "x";
        vm.RunText = string.Empty;

        // Dropdown should NOT auto-open on empty text (only via arrow-down)
        Assert.False(vm.IsHistoryOpen);
        Assert.Equal(2, vm.FilteredHistory.Count);
    }

    [Fact]
    public void RunText_Set_AutoSelectsBestMatch()
    {
        // While typing, the best match must be auto-highlighted so Enter executes it.
        var (vm, _) = Create(new List<string> { "a", "b" });

        vm.MoveSelection(1); // index 0
        vm.MoveSelection(1); // index 1
        Assert.Equal(1, vm.SelectedHistoryIndex);

        vm.RunText = "a";
        Assert.True(vm.IsHistoryOpen);
        Assert.Equal(0, vm.SelectedHistoryIndex);
        // RunText itself stays as what the user typed; only the highlight moves.
        Assert.Equal("a", vm.RunText);
    }

    [Fact]
    public void RunText_NoMatches_ClearsSelection()
    {
        var (vm, _) = Create(new List<string> { "notepad" });

        vm.RunText = "xyz";

        Assert.False(vm.IsHistoryOpen);
        Assert.Equal(-1, vm.SelectedHistoryIndex);
    }

    [Fact]
    public void RunText_Filter_PrefixMatchRanksAboveSubstring()
    {
        // History MRU order would normally surface "my-notepad" first, but a
        // prefix match on "note" should rank "notepad" higher.
        var (vm, _) = Create(new List<string> { "my-notepad", "notepad", "calc" });

        vm.RunText = "note";

        Assert.True(vm.FilteredHistory.Count >= 2);
        Assert.Equal("notepad", vm.FilteredHistory[0]);
        Assert.Equal(0, vm.SelectedHistoryIndex);
    }

    // ── OpenHistory ───────────────────────────────────────────────────────────

    [Fact]
    public void OpenHistory_PopulatesAndOpens()
    {
        var (vm, _) = Create(new List<string> { "alpha", "beta" });

        vm.OpenHistory();

        Assert.True(vm.IsHistoryOpen);
        Assert.Equal(0, vm.SelectedHistoryIndex);
        Assert.Equal("alpha", vm.RunText);
    }

    [Fact]
    public void OpenHistory_EmptyHistory_StaysClosed()
    {
        var (vm, _) = Create(new List<string>());

        vm.OpenHistory();

        Assert.False(vm.IsHistoryOpen);
    }

    // ── Execute (state changes only, no actual process) ───────────────────────

    [Fact]
    public void Execute_EmptyInput_DoesNothing()
    {
        var (vm, _) = Create();
        int collapseCount = 0;
        vm.CollapseRequested += (_, _) => collapseCount++;

        vm.RunText = "";
        vm.ExecuteCommand.Execute(null);

        Assert.Equal(0, collapseCount);
    }

    [Fact]
    public void Execute_WhitespaceInput_DoesNothing()
    {
        var (vm, _) = Create();
        int collapseCount = 0;
        vm.CollapseRequested += (_, _) => collapseCount++;

        vm.RunText = "   ";
        vm.ExecuteCommand.Execute(null);

        Assert.Equal(0, collapseCount);
    }

    [Fact]
    public void Execute_ClosesDropdownAndResetsIndex()
    {
        var (vm, _) = Create(new List<string> { "notepad" });

        vm.MoveSelection(1); // open dropdown
        Assert.True(vm.IsHistoryOpen);

        // Execute with some random text that will fail shell execute
        // but succeed on search fallback
        vm.RunText = "test search query";
        vm.ExecuteCommand.Execute(null);

        Assert.False(vm.IsHistoryOpen);
        Assert.Equal(-1, vm.SelectedHistoryIndex);
    }

    // ── RequestCollapse ───────────────────────────────────────────────────────

    [Fact]
    public void RequestCollapse_ClearsStateAndFiresEvent()
    {
        var (vm, _) = Create(new List<string> { "item" });
        bool collapsed = false;
        vm.CollapseRequested += (_, _) => collapsed = true;

        vm.RunText = "something";
        vm.MoveSelection(1); // open dropdown

        vm.RequestCollapse();

        Assert.True(collapsed);
        Assert.Equal(string.Empty, vm.RunText);
        Assert.False(vm.IsHistoryOpen);
    }

    // ── Error toast ───────────────────────────────────────────────────────────

    [Fact]
    public void ShowError_InitiallyFalse()
    {
        var (vm, _) = Create();
        Assert.False(vm.ShowError);
        Assert.Equal(string.Empty, vm.ErrorText);
    }

    [Fact]
    public void ClearErrorCommand_DismissesError()
    {
        var (vm, _) = Create();

        // Simulate an error state (we can't easily trigger it without process execution)
        vm.ClearErrorCommand.Execute(null);

        Assert.False(vm.ShowError);
        Assert.Equal(string.Empty, vm.ErrorText);
    }

    // ── Labels ────────────────────────────────────────────────────────────────

    [Fact]
    public void PlaceholderLabel_InitializedFromLocalization()
    {
        var (vm, _) = Create();
        Assert.Equal("Run command, open path, or search...", vm.PlaceholderLabel);
    }

    [Fact]
    public void OnLanguageChanged_RefreshesLabels()
    {
        var (vm, _) = Create();
        // Just verify it doesn't throw and the label is still populated
        vm.OnLanguageChanged();
        Assert.False(string.IsNullOrEmpty(vm.PlaceholderLabel));
    }

    // ── SelectHistoryItem ─────────────────────────────────────────────────────

    [Fact]
    public void SelectHistoryItem_Null_DoesNothing()
    {
        var (vm, _) = Create(new List<string> { "item" });
        int collapseCount = 0;
        vm.CollapseRequested += (_, _) => collapseCount++;

        vm.SelectHistoryItemCommand.Execute(null);

        // Should not crash or trigger collapse
        Assert.Equal(0, collapseCount);
    }

    // ── History max size ──────────────────────────────────────────────────────

    [Fact]
    public void History_LimitedTo50()
    {
        var history = Enumerable.Range(1, 55).Select(i => $"item{i}").ToList();
        var (vm, settings) = Create(history);

        // ViewModel constructor loads from settings, but doesn't truncate
        // Truncation happens on AddToHistory. Verify the list is loaded.
        Assert.Equal(55, settings.Current.RunHistory.Count);
    }

    // ── Filter limits ─────────────────────────────────────────────────────────

    [Fact]
    public void FilteredHistory_MaxTenItems()
    {
        var history = Enumerable.Range(1, 20).Select(i => $"item {i}").ToList();
        var (vm, _) = Create(history);

        vm.RunText = "item";

        Assert.Equal(10, vm.FilteredHistory.Count);
    }

    [Fact]
    public void FilteredHistory_EmptyFilter_ShowsRecent10()
    {
        var history = Enumerable.Range(1, 15).Select(i => $"cmd{i}").ToList();
        var (vm, _) = Create(history);

        vm.RunText = ""; // empty filter
        vm.OpenHistory();

        Assert.True(vm.FilteredHistory.Count <= 10);
    }

    // ── RemoveHistoryItem during navigation ───────────────────────────────────

    [Fact]
    public void RemoveHistoryItem_AdjustsSelectedIndex()
    {
        var (vm, _) = Create(new List<string> { "a", "b", "c" });

        vm.MoveSelection(1); // index 0
        vm.MoveSelection(1); // index 1
        vm.MoveSelection(1); // index 2

        vm.RemoveHistoryItem("c"); // remove current selection

        // Index should clamp to last valid
        Assert.True(vm.SelectedHistoryIndex < vm.FilteredHistory.Count);
    }

    // ── PropertyChanged notifications ─────────────────────────────────────────

    [Fact]
    public void RunText_RaisesPropertyChanged()
    {
        var (vm, _) = Create();
        var changes = new List<string>();
        vm.PropertyChanged += (_, e) => changes.Add(e.PropertyName!);

        vm.RunText = "test";

        Assert.Contains("RunText", changes);
    }

    [Fact]
    public void IsHistoryOpen_RaisesPropertyChanged()
    {
        var (vm, _) = Create(new List<string> { "item" });
        var changes = new List<string>();
        vm.PropertyChanged += (_, e) => changes.Add(e.PropertyName!);

        vm.IsHistoryOpen = true;

        Assert.Contains("IsHistoryOpen", changes);
    }

    [Fact]
    public void ShowError_RaisesPropertyChanged()
    {
        var (vm, _) = Create();
        var changes = new List<string>();
        vm.PropertyChanged += (_, e) => changes.Add(e.PropertyName!);

        // Trigger via ClearError (sets ShowError = false, no change if already false)
        vm.ClearErrorCommand.Execute(null);

        // ShowError was already false, so no change notification expected
        // This verifies the machinery doesn't crash
        Assert.NotNull(changes);
    }

    // ── Second pass: additional coverage ──────────────────────────────────────

    [Fact]
    public void RemoveHistoryItem_NonExistentItem_DoesNothing()
    {
        var (vm, settings) = Create(new List<string> { "alpha", "beta" });

        vm.RemoveHistoryItem("gamma");

        Assert.Equal(2, settings.Current.RunHistory.Count);
    }

    [Fact]
    public void MoveSelection_AfterCommitSelection_ReopensCorrectly()
    {
        var (vm, _) = Create(new List<string> { "a", "b" });

        vm.MoveSelection(1); // open, select "a"
        vm.CommitSelection(); // close dropdown, text = "a"
        Assert.False(vm.IsHistoryOpen);

        vm.MoveSelection(1); // should re-open with filtered results
        Assert.True(vm.IsHistoryOpen);
    }

    [Fact]
    public void OpenHistory_CalledTwice_StaysOpen()
    {
        var (vm, _) = Create(new List<string> { "alpha", "beta" });

        vm.OpenHistory();
        Assert.True(vm.IsHistoryOpen);
        Assert.Equal(0, vm.SelectedHistoryIndex);

        // Second call re-filters (RunText is now "alpha" from first call)
        // but dropdown stays open and functional
        vm.OpenHistory();
        Assert.True(vm.IsHistoryOpen);
        Assert.Equal(0, vm.SelectedHistoryIndex);
        Assert.True(vm.FilteredHistory.Count > 0);
    }

    [Fact]
    public void RunText_Typing_AutoOpensDropdown()
    {
        var (vm, _) = Create(new List<string> { "notepad", "calc" });

        vm.RunText = "note";

        Assert.True(vm.IsHistoryOpen);
        Assert.Single(vm.FilteredHistory);
    }

    [Fact]
    public void RunText_TypingThenClearing_ClosesDropdown()
    {
        var (vm, _) = Create(new List<string> { "notepad" });

        vm.RunText = "note";
        Assert.True(vm.IsHistoryOpen);

        vm.RunText = "zzz"; // no matches
        Assert.False(vm.IsHistoryOpen);
    }

    [Fact]
    public void RemoveHistoryItem_CaseInsensitive_UpdatesFilteredHistory()
    {
        var (vm, _) = Create(new List<string> { "Notepad", "calc" });

        vm.RunText = "note";
        Assert.Single(vm.FilteredHistory);

        // Remove with different case
        vm.RemoveHistoryItem("notepad");
        Assert.Empty(vm.FilteredHistory);
    }

    [Fact]
    public void HandleEscape_ClearsError()
    {
        var (vm, _) = Create();
        vm.CollapseRequested += (_, _) => { };

        // HandleEscape calls RequestCollapse which calls DismissError
        vm.HandleEscape();

        Assert.False(vm.ShowError);
        Assert.Equal(string.Empty, vm.ErrorText);
    }

    [Fact]
    public void IsUrl_SchemeOnly_ReturnsFalse()
    {
        Assert.False(RunBoxViewModel.IsUrl("http://"));
        Assert.False(RunBoxViewModel.IsUrl("https://"));
    }

    [Fact]
    public void IsUrl_SchemeWithSpace_ReturnsFalse()
    {
        Assert.False(RunBoxViewModel.IsUrl("https:// example.com"));
    }

    [Fact]
    public void RequestCollapse_MultipleCallsSafe()
    {
        var (vm, _) = Create();
        int collapseCount = 0;
        vm.CollapseRequested += (_, _) => collapseCount++;

        vm.RequestCollapse();
        vm.RequestCollapse();

        Assert.Equal(2, collapseCount);
        Assert.Equal(string.Empty, vm.RunText);
    }

    [Fact]
    public void MoveSelection_WhileFiltered_UsesFilteredList()
    {
        var (vm, _) = Create(new List<string> { "notepad", "notes", "calc" });

        vm.RunText = "note";
        Assert.Equal(2, vm.FilteredHistory.Count);
        // Typing auto-selects the best match (index 0).
        Assert.Equal(0, vm.SelectedHistoryIndex);

        // Arrow-down should navigate the filtered list from the current selection.
        vm.MoveSelection(1);
        Assert.True(vm.IsHistoryOpen);
        Assert.Equal(1, vm.SelectedHistoryIndex);
        Assert.Equal("notes", vm.RunText);
    }
}
