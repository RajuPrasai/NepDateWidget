# NepDate Widget - AI Agent Instructions

This file is loaded automatically by GitHub Copilot in every chat for this repo.
Read it fully before responding to any request.

---

## Project identity

WPF desktop widget targeting Windows 10 1803+. Displays Nepali (BS) calendar, date conversion, unit tools, banking tools, network scanner, RunBox launcher, and document manager. Distributed exclusively through the Microsoft Store as an MSIX package. Single external NuGet: `NepDate 2.0.7`.

**Stack:** .NET 10 (`net10.0-windows10.0.17763.0`), C# with `nullable enable` and `ImplicitUsings`, WPF (`UseWPF=true`), MVVM with a hand-rolled `ViewModelBase`.

**Test baseline:** All tests, 0 failures. Every C# change must keep this green. Run: `dotnet test tests/NepDateWidget.Tests/NepDateWidget.Tests.csproj --no-build -q`

**Website:** The companion website lives in `docs/` and is served via GitHub Pages. It is the primary acquisition channel for new users and has the same quality bar as the C# codebase. Changes to the website are just as consequential as changes to the app.

---

## Design language and product philosophy

The app and website share a single identity. Any change to either must be consistent with the other.

**Target user.** Someone who works with both Bikram Sambat (BS) and Gregorian (AD) dates daily - government paperwork, bank documents, payroll, family events. They may use the app in English or Nepali. They expect instant, offline results without switching between apps. Every feature decision should pass the test: does this serve that specific person?

**Core principle.** Function first. Every element earns its place by doing something useful. Decoration for its own sake is wrong. Density is a feature, not a problem - the target user has real tasks and wants information fast.

**App visual language.** Dense, information-rich UI. No empty decorative spacing. All colors through `DynamicResource` theme brushes - never hardcoded. Light and dark modes are first-class. Typography is functional: ClearType rendering, explicit `TextFormattingMode="Display"` where it matters. Animations respect the `AnimationEnabled` flag always.

**Website visual language.** Same density principle applied to web content. No filler headings or padding paragraphs. Every section either informs the user or signals relevance to search engines - ideally both. Features described on the website must match what the app actually does. Screenshots must be current.

**Keyword identity.** The product lives at the intersection of "nepali date converter", "nepali calendar", and "nepali patro". These are how the target user searches. They must appear in H1s, meta descriptions, schema, and body copy on every relevant page. Do not optimize for generic terms at the cost of these specific ones.

---

## Architecture overview

Two WPF windows with distinct lifetimes:
- `MainWindow` - permanent mini pill. Always alive from startup to exit. Owns: Win32 hooks, hotkey registration, topmost timers, fullscreen detection, window position persistence (drag handling, initial position snap).
- `ExpandedShellWindow` - destroyed on every collapse, recreated on every expand. Hosts all 9 tabs. Reference held as `_shell` in `MainWindow`; null when collapsed.

**ViewModels are singletons.** `CalendarViewModel`, `MainViewModel`, and all tab VMs survive the window lifecycle. Views subscribe to VM events via `DataContextChanged` and must unsubscribe via `Unloaded`. This is not optional - it is the fix for the multi-month navigation jump bug.

**Expand cost is dominated by WPF visual tree creation.** `ExpandedShellWindow` is destroyed on every collapse and recreated on every expand. The `CachedTabControl` eagerly loads all 9 tab visual trees on every `OnApplyTemplate()` call. Inactive tabs use `Visibility.Collapsed` (no layout/render cost), but all binding initialization and object creation still happens. This is the inherent structural cost of the destroy-on-collapse architecture. Do not propose pre-warming or lazy tab loading without explicit discussion - the RAM saving (40-80 MB) on collapse is the deliberate trade-off.

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
- **`ThemeService.cs`** - `ApplyPalette()` computes all theme brushes algorithmically from three source values (Background, Foreground, Accent) and writes them individually via `SetBrush(key, color)` which sets `Application.Current.Resources[key] = new SolidColorBrush(color)`. New theme brushes must be added at the end of `ApplyPalette()` and also given a fallback value in `DefaultTheme.xaml`. `OverrideHighlightColor` re-applies accent overrides after every palette switch.
- **`MainViewModel.cs`** - `_syncingFromSettings` guard in all property setters. The `try/finally` in `SyncFromSettings()` is load-bearing. Do not remove or bypass it. `_todayInfo` lazy cache - only invalidated in `RefreshCopyLabels()` when `DateTime.Today` changes.
- **`ReminderService.cs`** - `WouldRecurOnDate` Monthly case uses O(1) `Math.Min` formula. Do not reintroduce a loop. `CheckAndFireDueReminders` iterates `_reminders` directly (no `.ToList()`).
- **`LocalizationService.cs`** - `_loadedDefaults` is cached after first read. `MergeMissingFromDefaults()` uses the cache.
- **`CalendarViewModel.cs`** - `RefreshGrid()` fast path (in-place `Update()`) and slow path (delta add/remove). `HasReminders` and `HasNote` are set from batch `GetHasRemindersForMonth`/`GetHasNotesForMonth` queries computed once before the loop, not per-cell. The assignments cover all cells unconditionally (padding and current-month alike) - gating on `IsCurrentMonth` leaves stale dots on padding cells after navigation.

---

## Performance rules

**`new NepaliDate(y, m, d)` does an internal calendar table lookup.** Avoid calling it more than once per calendar cell. Use `GetCellData(y, m, d)` on `INepaliDateAdapter` which wraps one call and extracts all cell data from it. Note: `NepaliDate` is a `readonly partial struct` with O(1) flat-array lookup (~5 ns, zero heap allocations) - the concern is redundant lookups per cell, not allocation pressure or GC. Do not treat it as a bottleneck beyond the one-per-cell rule.

**Batch per-month service queries.** Any service operation that checks a property for individual days must be batched for the whole month before any per-cell loop. The pattern: call a method that returns `HashSet<int>` of matching day numbers for the month, then use `Contains(day.Day)` inside the loop. Never call per-key lookups (e.g., `GetNote(key)`, `HasRemindersForDate(date)`) inside a loop over 42 cells. When adding a new per-day feature, add the batch variant to both the service interface and all implementations before wiring it to the calendar.

**`CalendarDayViewModel.Update()` fires PropertyChanged conditionally.** Before replacing `_day`, capture all delegated property values. After replacing, only call `OnPropertyChanged` for values that actually changed. Column-fixed properties (`IsSaturday`, `IsSunday`) never change across navigation and should never fire. `IsToday` flips for at most 2 cells per navigation. Firing unconditionally sends up to 378 WPF binding updates per navigation - almost all no-ops.

**`RelayCommand` and `CommandManager.RequerySuggested`.** Commands without a `canExecute` delegate must not register with `CommandManager.RequerySuggested`. The `CanExecuteChanged.add/remove` accessors are no-ops when `_canExecute is null`. With 9 cached tab visual trees alive simultaneously, unrestricted registration means every keyboard or mouse input triggers `CanExecute()` across every button in every tab. New commands follow the opt-in pattern already implemented in `RelayCommand.cs`. Commands that control button availability through a separate `IsEnabled` binding (rather than via `CanExecute`) do not need a `canExecute` delegate at all.

**Calendar grid refresh.** `ObservableCollection<CalendarDayViewModel>` must not be `Clear()`-ed and rebuilt. The fast path calls `vm.Update()` in-place - zero `CollectionChanged` events, zero container teardown. Any proposal that reverts to `Clear()` + re-add is a regression.

**Theme apply.** `ThemeService.ApplyPalette()` writes each brush individually to `Application.Current.Resources`. New brushes must be derived inside `ApplyPalette()` and have a `DefaultTheme.xaml` fallback so the XAML designer does not break.

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

**Design language - accent-forward surfaces.** The project follows an accent-forward design: every element the user can interact with carries an accent tint in its resting state. `WidgetInputBrush` (neutral gray) is reserved for non-interactive containers such as card rows, progress panels, info notices, and file list wrappers. It must not be used as the background for any clickable, tappable, or focusable element in new work.
The brush hierarchy for interactive elements is:
- Primary actions (Compress, Save, Confirm): `WidgetAccentBrush` background, `WidgetDayTodayTextBrush` foreground.
- Secondary actions, form cancel/browse/remove buttons, small icon action buttons, search bar input containers, drop zones: `WidgetAccentBrushLight` background (35% accent blended into background - computed by `ThemeService` and must have a `DefaultTheme.xaml` fallback).
- Ghost / mode-selector buttons (transparent + border): `Transparent` background, `WidgetHoverBrush` on hover.
- Danger actions (delete): `WidgetErrorBrush` background.
This hierarchy applies consistently to every view, every tab, every new feature. When adding any interactive element, pick from this hierarchy without exception.

**Interaction feedback rules.** Hover and press states must follow a consistent pattern across the app:
- Solid-fill buttons (accent, secondary, danger) reduce `Opacity` to `0.75`–`0.85` on hover rather than changing background color. This preserves the brush across light and dark themes without extra triggers.
- Icon grid navigation tiles and navigation back buttons use `WidgetAccentBrushLight` as their hover background (replacing `WidgetHoverBrush`).
- Drop zones use `WidgetAccentBrushLight` as their resting background and `WidgetHoverBrush` on hover so the hover shift clearly signals a valid drag target.
- List item suggestion buttons and context menu items (`WidgetMenuItemStyle`, `ThemedTextBoxContextMenu` MenuItem) use `WidgetAccentBrushLight` on hover.
- The mini pill (`WidgetBorder` in `MainWindow`) must not respond to hover with any color or border change. Physical micro-cues only: a subtle upward translate (`PillLift.Y = -2`, 100ms ease-out) on enter, reversed on leave (150ms). Both must respect `ViewModel.AnimationEnabled`.

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
- `RelayCommand` is used for commands. `CommandManager.RequerySuggested` fires on every user input globally. Keep `CanExecute` implementations cheap - O(1) field reads only. Commands without a `canExecute` delegate must not register with `RequerySuggested` at all (see Performance rules).
- Do not add services or dependencies to a ViewModel constructor without updating `App.xaml.cs`. There is no DI container; everything is wired manually.

---

## Testing rules

- Before proposing any code change, consider what tests already cover the affected area. The test project is `tests/NepDateWidget.Tests/`.
- Test files use: `FakeNepaliDateAdapter`, `FakeReminderService`, `FakeNotesService`, `FakeCalendarService`, `FakeLocalizationService` - all in `tests/NepDateWidget.Tests/Services/` and `Helpers/`.
- `FakeReminderService` must have a `GetHasRemindersForMonth` stub (`=> new()`). If adding a new method to `IReminderService`, add the stub there too.
- When adding a batch query method to any service interface, add the corresponding stub to every fake in the test project. Stubs that return `new()` (empty set) are correct for tests that don't exercise that feature. Any fake whose tests seed data and verify dot/indicator visibility must implement the real lookup logic - an empty stub will silently cause those tests to fail with wrong expected values.
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

## Website & SEO maintenance

The `docs/` folder is a static website served via GitHub Pages. The rules below apply whenever touching any file in `docs/`.

**HTML correctness.** `<title>` text content must use `&amp;` not bare `&`. Attribute values containing `&` should also use `&amp;`. HTML entity references in visible text (`&harr;`, `&amp;`, etc.) are fine and render correctly.

**Schema.org JSON-LD.** Every page has one or more `<script type="application/ld+json">` blocks. These must remain valid JSON after any edit - no duplicate keys, no trailing commas. Validate mentally after every change. `alternateName` should be an array when the entity has more than one known name or alias.

**Datetime fields in structured data.** `uploadDate` on `VideoObject` is typed as `DateTime` by Google, not `Date`. It requires a full ISO 8601 datetime with timezone: `"YYYY-MM-DDThh:mm:ss+05:45"`. A date-only string like `"2026-01-01"` will trigger a Google Search Console warning. `datePublished` and `dateModified` on `WebPage`, `SoftwareApplication`, etc. are typed as `Date` and accept date-only strings - no timezone required for those.

**Date freshness.** Whenever any page in `docs/` is modified, two things must be updated together: the `"dateModified"` field in that page's `WebPage` schema block, and the `<lastmod>` entry for that URL in `sitemap.xml`. If one is updated without the other, Google sees an inconsistency.

**Required meta tags on every page.** All six pages must carry: `application-name`, `geo.region` (`NP`), `geo.placename` (`Nepal`), `og:locale` (`en_US`), `og:locale:alternate` (`ne_NP`). These are invisible to users and signal Nepal geo-relevance and Nepali-language audience to search platforms.

**SEO structure.** Every page's `<title>` must front-load the primary keyword, not the brand name. Every page's H1 must contain at least one of the primary target keywords. The primary keyword set is: `nepali date converter`, `nepali calendar`, `nepali patro`, `bs to ad`, `ad to bs`. Do not introduce SEO changes that remove or bury these terms to make room for generic phrasing.

**Sitemap.** `sitemap.xml` lives at `docs/sitemap.xml`. The `<lastmod>` date for any page must match or be newer than the date in that page's `WebPage` schema `dateModified`. Update the sitemap whenever any page is changed.

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
8. **Go deeper before concluding.** A surface reading is a starting point, not a conclusion. If something looks correct at a glance, verify it against the actual spec, file, or external documentation before asserting it is correct. This applies equally to C# behavior, Schema.org validity, Google Search Console rules, Store submission requirements, and HTML correctness.
9. **For external systems, verify against the official specification.** Do not assume what is valid for SEO markup, structured data, store submissions, or APIs - fetch or read the authoritative source before proposing a fix.
10. **After any batch edit, verify structural integrity.** For JSON-LD: no duplicate keys, valid syntax. For HTML: entity escaping in text content (`&amp;` not `&` in `<title>` and attribute values). For XML: well-formedness. For sitemap and schema dates: they must stay in sync.

When uncertain, read the file. Do not guess.

---

## Common pitfalls (things that have actually broken this codebase)

- Adding a second `settingsService.Load()` anywhere causes double file reads and duplicate default merges.
- Subscribing to a CalendarViewModel event in `DataContextChanged` without a corresponding `Unloaded` unsubscribe causes multi-month navigation jumps after expand/collapse cycles.
- Gating `HasReminders = ...` inside `if (day.IsCurrentMonth)` leaves stale dots on padding cells after navigation.
- Calling `UpdateVisibleEventCount()` without firing `PropertyChanged` for `VisibleEvents` when clearing leaves stale event text on cells.
- Using `Clear()` + `AddRange()` to refresh the calendar grid destroys 42 WPF containers and rebuilds them - correct behavior, but a regression in performance given the template complexity.
- Writing to a data file path with `File.WriteAllText` instead of `AtomicFile.WriteAllText` risks file corruption on crash or power loss.
- `new NepaliDate(y, m, d)` called multiple times per cell during `RefreshGrid()` - use `GetCellData()` instead.
- Sub-views embedded inside `MoreView` each have their own explicit `DataContext`. Using `{Binding IsSubViewX}` on them resolves against the wrong context, showing multiple sub-views simultaneously. Always use `{Binding DataContext.IsSubViewX, ElementName=RootMoreView, FallbackValue=Collapsed}`.
- WPF drag-drop suppression via a boolean flag is unreliable. `Drop` fires on the `UserControl`; `MouseLeftButtonUp` fires on the child drop zone `Border`. Event order is not guaranteed, so the flag may not be set when the handler runs. Correct approach: track `MouseLeftButtonDown` on the drop zone element. A real click produces both Down and Up on the element; a drag-drop only produces Up.
- Removing a method from a test file with imprecise context matching can silently delete the namespace declaration and class header, leaving orphaned method bodies that produce `CS0106`/`CS8803` build errors. Always read at least 5 lines before and after the target region before applying a removal.
- Using a date-only string (`"2026-01-01"`) for `uploadDate` in a `VideoObject` schema. Google types this field as `DateTime`, not `Date` - a date-only value triggers a Search Console warning. Always use full ISO 8601 with timezone.
- Modifying any page in `docs/` without updating both `WebPage.dateModified` in that page's JSON-LD and `<lastmod>` in `sitemap.xml`. The two must stay in sync or Google's freshness signals for the page are stale.
- Bare `&` in `<title>` tag text content. Must be `&amp;`. HTML5 parsers are lenient but Search Console's HTML report flags it.
- Setting `alternateName` to a single string in Schema.org JSON-LD when the entity has multiple known names. Use an array: `"alternateName": ["Name One", "Name Two"]`.
- Proposing an SEO fix based on a surface reading of the warning message without checking what the spec actually requires. Always verify the field type and required format against the official Google documentation or Schema.org spec before concluding what the correct value is.
- Assuming `NepaliDate` allocation is a bottleneck - it is not. `readonly partial struct`, O(1) lookup, ~5 ns, zero heap allocations. The one-per-cell rule is about avoiding redundant calendar lookups, not GC pressure. Do not optimise away from it for the wrong reason.
- Per-cell service queries inside any loop over calendar cells - reminders, notes, events, or any future per-day feature must be batched for the whole month before the loop. A `HashSet<int>` for the month computed once, then `Contains(day.Day)` per cell, is the required pattern.
- Firing `OnPropertyChanged` unconditionally in `CalendarDayViewModel.Update()` - column-fixed properties (`IsSaturday`, `IsSunday`) are determined by grid position and never change during navigation. Unconditional firing of these and other unchanged properties on all 42 cells per navigation is pure overhead.
- Diagnosing a performance regression in expand/open speed as being caused by VM or service changes - the dominant expand cost is WPF visual tree creation for all 9 tab views. This runs on every expand because the window is destroyed on collapse. VM-side changes (fewer PropertyChanged events, fewer RequerySuggested registrations) reduce cost; they cannot increase it. Investigate visual tree depth, binding count, or layout passes before concluding a regression exists.
