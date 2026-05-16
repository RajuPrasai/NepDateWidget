# NepDate Widget - AI Agent Instructions

This file is loaded automatically by GitHub Copilot in every chat for this repo.
Read it fully before responding to any request.

---

## Project identity

WPF desktop widget targeting Windows 10 1803+. Displays Nepali (BS) calendar, date conversion, unit tools, banking tools, network scanner, RunBox launcher, and document manager. Distributed exclusively through the Microsoft Store as an MSIX package. Single external NuGet: `NepDate 2.0.7`.

**Stack:** .NET 10 (`net10.0-windows10.0.17763.0`), C# with `nullable enable` and `ImplicitUsings`, WPF (`UseWPF=true`), MVVM with a hand-rolled `ViewModelBase`.

**Test baseline:** 1731 tests, 0 failures. Every change must keep this green. Run: `dotnet test tests/NepDateWidget.Tests/NepDateWidget.Tests.csproj --no-build -q`

---

## Architecture overview

Two WPF windows with distinct lifetimes:
- `MainWindow` - permanent mini pill. Always alive from startup to exit. Owns: Win32 hooks, hotkey registration, topmost timers, fullscreen detection, window position persistence (drag handling, initial position snap).
- `ExpandedShellWindow` - destroyed on every collapse, recreated on every expand. Hosts all 9 tabs. Reference held as `_shell` in `MainWindow`; null when collapsed.

**ViewModels are singletons.** `CalendarViewModel`, `MainViewModel`, and all tab VMs survive the window lifecycle. Views subscribe to VM events via `DataContextChanged` and must unsubscribe via `Unloaded`. This is not optional - it is the fix for the multi-month navigation jump bug.

**Service composition** is done entirely in `App.xaml.cs`. Every service is constructed once and passed down by constructor injection. `MainViewModel` receives services; it does not create them or call `Load()` itself.

**Startup sequence (order matters):**
1. `Log.Initialize`
2. Single-instance mutex check
3. `settingsService.Load()` - only here, nowhere else
4. All other services loaded in order
5. `new MainViewModel(...)` - no Load() inside
6. `new MainWindow(...)` - show

---

## Hard-stop areas - do not modify without explicit instruction

These areas are working correctly. Touching them risks breaking widget positioning, fullscreen behavior, or multi-monitor handling in ways that are not reproducible in tests.

| Area | Location | Why hands-off |
|---|---|---|
| Win+D / taskbar ownership | `MainWindow.xaml.cs` - `EnsureShellOwner()`, `SetWindowPos(HWND_TOPMOST)` | Governs whether the pill stays above the taskbar after Win+D |
| WinEvent foreground hook | `MainWindow.xaml.cs` - `_winEventHook`, `_winEventProc`, `OnForegroundChanged` | Drives HWND cache refresh and topmost recheck on every foreground change |
| Fullscreen detection | `MainWindow.xaml.cs` - `IsForegroundFullscreen()`, `_fullscreenTimer` (600ms), `_topmostTimer` (3s) | If broken, widget overlaps fullscreen games/videos |
| Shell_TrayWnd HWND cache | `MainWindow.xaml.cs` - `GetShellTrayWnd()`, `_cachedShellTrayHwnd`, `Win32Interop.IsWindow` | Cache logic is deliberate: only re-calls `FindWindow` when `IsWindow` returns false |
| Shell expand/collapse lifecycle | `MainWindow.xaml.cs` - `OnExpandStateChanged()`, `AnimateAndHide()`, `ForceClose()` | Destroy-on-collapse is intentional (frees 40-80 MB of WPF visual tree - containers, rendered bitmaps, layout state). ViewModels are singletons and survive. The sequence `_shell = null` → animate → `ForceClose()` on `IsVisibleChanged` is precise |
| RunBox hotkey | `MainWindow.xaml.cs` - `RegisterRunBoxHotkey()`, `HOTKEY_ID_RUNBOX` | Global Win32 hotkey; re-registration logic is tied to settings change events |
| MSIX packaging identity | `Package.appxmanifest`, `store-identity.ps1`, `build-store.ps1` | Partner Center values, publisher hash, family name - any mismatch breaks Store submission |

If a request would require changing these, stop and say so clearly. Do not propose a workaround that indirectly touches them.

---

## Soft-warning areas - propose changes but flag the risk

Changes here are fine but must be explicitly reviewed before accepting.

- **`SettingsService.cs`** - `Load()`, `MergeNewSettingKeys()`, `Save()`, `_cachedDefaults`. The double-read fix (passing `defaultJson` through) is deliberate. The debounced reloader has a self-write guard. Do not add a second `Load()` call anywhere.
- **`ThemeService.cs`** - `ApplyPalette()` writes all 22 brushes into a single `ResourceDictionary` slot (`_themeResources`). Do not revert to per-key writes on `Application.Current.Resources`.
- **`MainViewModel.cs`** - `_syncingFromSettings` guard in all property setters. The `try/finally` in `SyncFromSettings()` is load-bearing. Do not remove or bypass it. `_todayInfo` lazy cache - only invalidated in `RefreshCopyLabels()` when `DateTime.Today` changes.
- **`ReminderService.cs`** - `WouldRecurOnDate` Monthly case uses O(1) `Math.Min` formula. Do not reintroduce a loop. `CheckAndFireDueReminders` iterates `_reminders` directly (no `.ToList()`).
- **`LocalizationService.cs`** - `_loadedDefaults` is cached after first read. `MergeMissingFromDefaults()` uses the cache.
- **`CalendarViewModel.cs`** - `RefreshGrid()` fast path (in-place `Update()`) and slow path (delta add/remove). `HasReminders` and `HasNote` assignments are unconditional - this is the fix for stale padding cell dots.

---

## Performance rules

**`new NepaliDate(y, m, d)` is expensive.** It does an internal calendar table lookup. Do not call it more than once per calendar cell. The `GetCellData(y, m, d)` method on `INepaliDateAdapter` wraps one allocation and extracts everything from it. Always use this for per-cell data.

**Calendar grid refresh.** `ObservableCollection<CalendarDayViewModel>` must not be `Clear()`-ed and rebuilt. The fast path calls `vm.Update()` in-place - zero `CollectionChanged` events, zero container teardown. Any proposal that reverts to `Clear()` + re-add is a regression.

**Theme apply.** One `ResourceDictionary` swap per apply, not per key. `_themeResources` is replaced in `MergedDictionaries` as a single operation.

**Settings save.** Must happen at most once per user action. `_syncingFromSettings` prevents cascading saves from property setters during bulk sync.

**`UpdateVisibleEventCount()` in `CalendarDayViewModel`.** When clearing events (no events or display disabled), it must fire `OnPropertyChanged(nameof(VisibleEvents))` before returning. The silent-reset path is a bug; it is already fixed.

---

## UI/UX constraints

**Do not change visual output without explicit instruction.** This includes: colors, font sizes, spacing, border radii, animation timing, cell layout, mini-bar layout.

**DynamicResource is required** for all theme-aware brushes. Never use a hardcoded color or a `StaticResource` for a brush that is expected to change with the theme.

**`TextOptions.TextFormattingMode="Display"` and `TextOptions.TextRenderingMode="ClearType"`** are set at the `UserControl` level on views where text clarity matters. Do not remove these or override them with inferior settings.

**Animation flag.** Every animation must respect `ViewModel.AnimationEnabled`. If false, do not play animations - also reset any properties the animation would have left mid-state.

**Cell template.** The calendar day `DataTemplate` has ~15 bindings and multiple `DataTrigger`s per cell. Adding more triggers or bindings increases per-navigation cost directly. Prefer computing derived values in the ViewModel.

**`BitmapCache` on any constantly-changing element is banned.** It was removed from `DaysGrid` for this reason.

**`CachedTabControl`** preserves visual trees across tab switches within a session. Do not replace it with a standard `TabControl`.

**Slider scales.** Use 5-point scales (0..4, `Maximum="4"`) for quality/compression sliders. 3-point is too coarse; 100-point forces text input. Never pair a slider with a free-text numeric override - the slider is the only control. The shared `IosSlider` keyed style in `DefaultTheme.xaml` must be used via `{DynamicResource IosSlider}`.

**Tool completion UX.** Never require a manual "Start New Job" button after a job finishes. Auto-reset with a dispatcher delay is the correct pattern: 3 seconds on full success, 8 seconds on partial failure (longer gives the user time to read the error detail). Call `CancelPendingAutoReset()` at the entry of `AddFiles()` or any "start new job" path so a new drop/pick cancels a pending reset.

**Completion summaries must be specific.** Never use generic "Done" or "Completed". Show: how many files succeeded, how many failed (if any), total bytes saved, and final total size. Format: `"{n} file(s) done - saved {X} in total, new size is {Y}"`. For partial failure: `"{n} done, {m} failed - saved {X} in total, new size is {Y}"`.

**File list deduplication.** If the same file path is added to a file-list collection twice, replace the existing entry with the new one (reset to Pending state, refresh file size). Never accumulate duplicates - the list represents the current job, not a history.

**Optional controls belong inside collapsible panels.** For example, resize dimensions inside the Compression tool's advanced panel rather than as a separate top-level section. Keep the default view simple; reveal options only when the user explicitly expands.

**Icon button templates.** The `GridIconBtnStyle` (and any similar icon-tile button) must use a `ContentPresenter` inside the template root, not a nested `StackPanel`. A `StackPanel` with no `ContentPresenter` clips or ignores the button's content.

**Localization completeness.** Every tool view that has user-visible text must wire `ILocalizationService` through its ViewModel. Hardcoded strings in either XAML or a ViewModel are a defect, not a TODO. The pattern is: VM field `_loc`, constructor parameter, computed label properties (`public string X => _loc.Get("key")`), `OnLanguageChanged()` method that fires `OnPropertyChanged` for every label, wired in `MoreViewModel.OnLanguageChanged()`. New tool VMs must follow this pattern from day one. Tests that directly construct a tool VM must pass `MakeLoc()` as the localization argument.

**Drop zone visibility.** When a tool accepts file input and maintains a file list, the drop zone must hide once `HasFiles` is true - `Visibility="{Binding HasFiles, Converter={StaticResource InverseBoolToVis}}"`. The `AllowDrop="True"` on the `UserControl` handles additional files via drag-drop after the drop zone is hidden. Do not add an explicit "+ Add more files" button to the file list section.

**Primary action button consistency.** All primary action buttons across sibling tool views within the same tab group must use `Height="36"`. The foreground must use `{DynamicResource WidgetDayTodayTextBrush}`, never hardcoded `"White"` - the brush adapts correctly across light and dark themes.

**Progress section layout.** Progress sections must be consistent across tool views: a `Grid` with the localized job-title label (`ProgressTitleLabel`) left-aligned and `FontWeight="SemiBold"`, and the count label (`ProgressLabel`, e.g. "3 of 10") right-aligned at reduced opacity. Never show only the count label alone.

**Orphaned local styles.** Before shipping a view, verify that every `Style` defined in `UserControl.Resources` is actually referenced by at least one element in that view. Orphaned styles are a code smell and must be removed.

---

## Sub-view DataContext binding rule

Sub-views embedded inside `MoreView` (or any view that assigns an explicit `DataContext` to its children) have their own `DataContext` pointing to their own ViewModel. Simple `{Binding IsSubViewX}` resolves against the sub-view's own context, not the parent's, causing multiple sub-views to show simultaneously.

**Correct pattern for visibility bindings on embedded sub-views:**
```xml
Visibility="{Binding DataContext.IsSubViewCompression,
             ElementName=RootMoreView,
             Converter={StaticResource BoolToVis},
             FallbackValue=Collapsed}"
```
- `RootMoreView` must be the `x:Name` on the root `UserControl` of the containing view.
- `FallbackValue=Collapsed` is mandatory - without it, the element is visible during XAML parse before the binding resolves.
- `DataContext.PropertyName` path is required, not just `PropertyName`.

---

## Data persistence rules

All user data JSON files are written via `AtomicFile.WriteAllText()` (write to `.tmp`, then `File.Replace`). Never write directly with `File.WriteAllText` to a live data path.

Data paths are resolved through `AppPaths`. Never construct a data file path manually with `Environment.GetFolderPath` or hardcoded strings. The app has three modes: packaged (MSIX), unpackaged (dev), and portable (`portable.flag` beside EXE). `AppPaths` handles all three.

Shipped default configs (`Resources/configs/*.json`) are read-only at runtime. They are copied to AppData on first launch and merged on subsequent launches. Never write to them.

`DebouncedFileReloader` watches user data files for external edits. `SettingsService` has a self-write guard (`_lastSelfWriteTicks`) that suppresses the reload callback for 1 second after a `Save()` call, preventing the app from reloading its own writes. Do not remove this guard.

---

## MVVM rules

- `ViewModelBase.SetProperty<T>` uses `EqualityComparer<T>.Default`. It will not fire `PropertyChanged` if the value did not change. Rely on this - do not call `OnPropertyChanged` manually unless you have a derived property to notify.
- All view code-behind that subscribes to a VM event in `DataContextChanged` must unsubscribe in an `Unloaded` handler. This is mandatory because `ExpandedShellWindow` is destroyed on collapse.
- `RelayCommand` is used for commands. `CommandManager.RequerySuggested` fires on every user input globally. Keep `CanExecute` implementations cheap - O(1) field reads only.
- Do not add services or dependencies to a ViewModel constructor without updating `App.xaml.cs`. There is no DI container; everything is wired manually.

---

## Testing rules

- Before proposing any code change, consider what tests already cover the affected area. The test project is `tests/NepDateWidget.Tests/`.
- Test files use: `FakeNepaliDateAdapter`, `FakeReminderService`, `FakeNotesService`, `FakeCalendarService`, `FakeLocalizationService` - all in `tests/NepDateWidget.Tests/Services/` and `Helpers/`.
- `FakeReminderService` must have a `GetHasRemindersForMonth` stub (`=> new()`). If adding a new method to `IReminderService`, add the stub there too.
- When adding a bug fix, add a regression test that would have caught it.
- Do not skip or modify existing tests to make a change pass. If a test breaks, the change is wrong.

---

## Release workflow

Versioning: `MAJOR.MINOR.PATCH` from the user's perspective. Bump `<Version>` in `src/NepDateWidget/NepDateWidget.csproj` (three-part, no `v` prefix).

Release checklist:
1. Bump `<Version>` and `<FileVersion>` in the csproj.
2. Run `dotnet test` and a Release build locally.
3. Commit, tag `vX.Y.Z`, push with `--tags`.
4. GitHub Actions `Release` workflow builds the MSIX and publishes.
5. The workflow enforces that the tag version matches the csproj version.

`store-identity.ps1` is gitignored and contains Partner Center credentials. Never commit it. `build-store.ps1` calls `makeappx.exe` (requires Windows SDK).

Do not propose changes to `Package.appxmanifest` publisher identity, `store-identity.ps1` structure, or `build-store.ps1` unless explicitly asked.

---

## Self-check protocol

Before finalizing any response involving code changes:

1. **Read the file first.** Do not edit from memory. Use the file content that was actually read in this session.
2. **Check for side effects.** Does the change affect any of the soft-warning areas? Say so.
3. **Check for test coverage.** Is the changed behavior covered? If not, say so.
4. **Check the test baseline.** If the change touches a service interface (e.g., `IReminderService`), all fake implementations in the test project need updating too.
5. **Do not assume the current state matches a previous session.** Always read the file before proposing an edit.
6. **After editing a test file, run `dotnet build` before `dotnet test`.** A build failure in the test project will silently produce stale results from a prior assembly if `--no-build` is used. Structural edits (removing a method, modifying a class boundary) can corrupt the file without being obvious.
7. **Verify class/namespace structure after any removal.** When removing a test or code block, confirm the surrounding namespace declaration, class declaration, and all braces are intact. Read at least 10 lines before the removed region to validate.

When uncertain, read the file. Do not guess.

---

## Common pitfalls (things that have actually broken this codebase)

- Adding a second `settingsService.Load()` anywhere causes double file reads and duplicate default merges.
- Subscribing to a CalendarViewModel event in `DataContextChanged` without a corresponding `Unloaded` unsubscribe causes multi-month navigation jumps after expand/collapse cycles.
- Gating `HasReminders = ...` inside `if (day.IsCurrentMonth)` leaves stale dots on padding cells after navigation.
- Calling `UpdateVisibleEventCount()` without firing `PropertyChanged` for `VisibleEvents` when clearing leaves stale event text on cells.
- Using `Clear()` + `AddRange()` to refresh the calendar grid destroys 42 WPF containers and rebuilds them - correct behavior, but a regression in performance given the template complexity.
- Adding per-key brush writes to `ThemeService` fires 22 `ResourcesChanged` events instead of 1.
- Writing to a data file path with `File.WriteAllText` instead of `AtomicFile.WriteAllText` risks file corruption on crash or power loss.
- `new NepaliDate(y, m, d)` called multiple times per cell during `RefreshGrid()` - use `GetCellData()` instead.
- Sub-views embedded inside `MoreView` each have their own explicit `DataContext`. Using `{Binding IsSubViewX}` on them resolves against the wrong context, showing multiple sub-views simultaneously. Always use `{Binding DataContext.IsSubViewX, ElementName=RootMoreView, FallbackValue=Collapsed}`.
- WPF drag-drop suppression via a boolean flag is unreliable. `Drop` fires on the `UserControl`; `MouseLeftButtonUp` fires on the child drop zone `Border`. Event order is not guaranteed, so the flag may not be set when the handler runs. Correct approach: track `MouseLeftButtonDown` on the drop zone element. A real click produces both Down and Up on the element; a drag-drop only produces Up.
- Removing a method from a test file with imprecise context matching can silently delete the namespace declaration and class header, leaving orphaned method bodies that produce `CS0106`/`CS8803` build errors. Always read at least 5 lines before and after the target region before applying a removal.
