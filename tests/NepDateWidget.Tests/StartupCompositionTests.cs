using NepDateWidget.Models;
using NepDateWidget.Services;
using NepDateWidget.Tests.Services;
using NepDateWidget.ViewModels;

namespace NepDateWidget.Tests;

/// <summary>
/// Tests that the services and ViewModels introduced in Phase 1 (compression/resize)
/// can be constructed without throwing, and that the MoreViewModel navigation layer
/// works correctly with and without those services.
///
/// These tests guard against:
/// - Constructor parameter changes that are not propagated everywhere.
/// - Services that throw during construction (regression check).
/// - MoreViewModel navigation bugs where grid→sub-view transitions are silently swallowed.
/// </summary>
public sealed class StartupCompositionTests
{
    // ── Service construction ──────────────────────────────────────────────────

    [Fact]
    public void FileTypeService_CanBeConstructed()
    {
        var svc = new FileTypeService();
        Assert.NotNull(svc);
    }

    [Fact]
    public void ImageCompressionService_CanBeConstructed()
    {
        var svc = new ImageCompressionService();
        Assert.NotNull(svc);
    }

    [Fact]
    public void PdfCompressionService_CanBeConstructed()
    {
        var svc = new PdfCompressionService();
        Assert.NotNull(svc);
    }

    [Fact]
    public void JobOrchestrationService_CanBeConstructed_WithRealServices()
    {
        var image       = new ImageCompressionService();
        var pdf         = new PdfCompressionService();
        var orchestrator = new JobOrchestrationService(image, pdf);
        Assert.NotNull(orchestrator);
    }

    // ── ViewModel construction ────────────────────────────────────────────────

    [Fact]
    public void CompressionViewModel_CanBeConstructed_WithRealServices()
    {
        var fileType     = new FileTypeService();
        var image        = new ImageCompressionService();
        var pdf          = new PdfCompressionService();
        var orchestrator = new JobOrchestrationService(image, pdf);

        var vm = new CompressionViewModel(fileType, orchestrator, MakeLoc());
        Assert.NotNull(vm);
        Assert.False(vm.HasFiles);
        Assert.False(vm.IsJobRunning);
    }

    [Fact]
    public void ResizeViewModel_CanBeConstructed_WithRealServices()
    {
        var fileType     = new FileTypeService();
        var image        = new ImageCompressionService();
        var pdf          = new PdfCompressionService();
        var orchestrator = new JobOrchestrationService(image, pdf);

        var vm = new ResizeViewModel(fileType, orchestrator, MakeLoc());
        Assert.NotNull(vm);
        Assert.False(vm.HasFiles);
        Assert.False(vm.IsJobRunning);
    }

    [Fact]
    public void MoreViewModel_WithCompressionServices_ExposesNonNullSubVMs()
    {
        var loc          = MakeLoc();
        var fileType     = new FileTypeService();
        var image        = new ImageCompressionService();
        var pdf          = new PdfCompressionService();
        var orchestrator = new JobOrchestrationService(image, pdf);

        var vm = new MoreViewModel(loc, fileTypeService: fileType, jobOrchestrationService: orchestrator);

        Assert.NotNull(vm.Compression);
        Assert.NotNull(vm.Resize);
    }

    [Fact]
    public void MoreViewModel_WithoutCompressionServices_LeavesSubVMsNull()
    {
        var vm = new MoreViewModel(MakeLoc());
        Assert.Null(vm.Compression);
        Assert.Null(vm.Resize);
    }

    // ── Navigation: grid → sub-view (regression for the SetMode early-return bug) ──

    // The bug: _modeIndex was initialised to 1 (Notes) so that the existing tests
    // IsModeNotes=true passed. But SetMode(1) had an early-return guard `if (index ==
    // _modeIndex) return`, which prevented the first grid→Notes navigation from ever
    // setting CurrentSubView = "Notes". Fix: guard now also checks CurrentSubView != null.

    [Fact]
    public void NavigateToNotes_FromGrid_SetsSubView()
    {
        var vm = new MoreViewModel(MakeLoc());
        // Initially the grid is visible (no sub-view selected).
        Assert.True(vm.IsGridVisible, "Grid should be visible at startup");
        Assert.Null(vm.CurrentSubView);

        vm.NavigateToCommand.Execute("Notes");

        Assert.False(vm.IsGridVisible, "Grid should be hidden after navigation");
        Assert.Equal("Notes", vm.CurrentSubView);
        Assert.True(vm.IsSubViewNotes);
    }

    [Fact]
    public void NavigateToDocuments_FromGrid_SetsSubView()
    {
        var vm = new MoreViewModel(MakeLoc());
        Assert.True(vm.IsGridVisible);

        vm.NavigateToCommand.Execute("Documents");

        Assert.False(vm.IsGridVisible);
        Assert.Equal("Documents", vm.CurrentSubView);
        Assert.True(vm.IsSubViewDocuments);
    }

    [Fact]
    public void NavigateToReminders_FromGrid_SetsSubView()
    {
        var vm = new MoreViewModel(MakeLoc());
        Assert.True(vm.IsGridVisible);

        vm.NavigateToCommand.Execute("Reminders");

        Assert.False(vm.IsGridVisible);
        Assert.Equal("Reminders", vm.CurrentSubView);
        Assert.True(vm.IsSubViewReminders);
    }

    [Fact]
    public void NavigateToCompression_FromGrid_SetsSubView()
    {
        var loc          = MakeLoc();
        var fileType     = new FileTypeService();
        var image        = new ImageCompressionService();
        var pdf          = new PdfCompressionService();
        var orchestrator = new JobOrchestrationService(image, pdf);
        var vm           = new MoreViewModel(loc, fileTypeService: fileType, jobOrchestrationService: orchestrator);

        Assert.True(vm.IsGridVisible);

        vm.NavigateToCommand.Execute("Compression");

        Assert.False(vm.IsGridVisible);
        Assert.Equal("Compression", vm.CurrentSubView);
        Assert.True(vm.IsSubViewCompression);
    }

    [Fact]
    public void NavigateToResize_FromGrid_SetsSubView()
    {
        var loc          = MakeLoc();
        var fileType     = new FileTypeService();
        var image        = new ImageCompressionService();
        var pdf          = new PdfCompressionService();
        var orchestrator = new JobOrchestrationService(image, pdf);
        var vm           = new MoreViewModel(loc, fileTypeService: fileType, jobOrchestrationService: orchestrator);

        Assert.True(vm.IsGridVisible);

        vm.NavigateToCommand.Execute("Resize");

        Assert.False(vm.IsGridVisible);
        Assert.Equal("Resize", vm.CurrentSubView);
        Assert.True(vm.IsSubViewResize);
    }

    [Fact]
    public void GoBackCommand_FromSubView_RestoresGrid()
    {
        var vm = new MoreViewModel(MakeLoc());
        vm.NavigateToCommand.Execute("Notes");
        Assert.False(vm.IsGridVisible);

        vm.GoBackCommand.Execute(null);

        Assert.True(vm.IsGridVisible);
        Assert.Null(vm.CurrentSubView);
    }

    [Fact]
    public void NavigateToNotes_TwiceInARow_StaysOnNotes()
    {
        // After returning to grid and clicking Notes again, navigation must succeed.
        var vm = new MoreViewModel(MakeLoc());
        vm.NavigateToCommand.Execute("Notes");
        vm.GoBackCommand.Execute(null);          // back to grid
        vm.NavigateToCommand.Execute("Notes");   // navigate to Notes again

        Assert.Equal("Notes", vm.CurrentSubView);
        Assert.True(vm.IsSubViewNotes);
    }

    // ── App.xaml.cs wiring: MainViewModel passes services to MoreViewModel ───
    //
    // If the fileTypeService/jobOrchestrationService arguments were accidentally
    // removed from MainViewModel's MoreViewModel constructor call, the Compression
    // and Resize sub-VMs would be null. The feature would silently not work and
    // nothing else would throw. This test guards that wiring path explicitly.

    [Fact]
    public void MainViewModel_WithCompressionServices_WiresThemToMore()
    {
        var adapter      = new FakeNepaliDateAdapter();
        var calendar     = new CalendarService(adapter);
        var conversion   = new ConversionService(adapter);
        var loc          = new LocalizationService(TestPaths.DefaultLocalizationPath);
        var fileType     = new FileTypeService();
        var image        = new ImageCompressionService();
        var pdf          = new PdfCompressionService();
        var orchestrator = new JobOrchestrationService(image, pdf);

        var vm = new MainViewModel(
            new FakeSettingsService(),
            calendar, loc, conversion,
            new FakeThemeService(),
            new FakeAutoStartService(),
            fileTypeService: fileType,
            jobOrchestrationService: orchestrator);

        Assert.NotNull(vm.More.Compression);
        Assert.NotNull(vm.More.Resize);
    }

    [Fact]
    public void MainViewModel_WithoutCompressionServices_LeavesMoreSubVMsNull()
    {
        // Matches the "services not provided" path - e.g. if the service construction
        // in App.xaml.cs throws before the orchestrator is built.
        var adapter    = new FakeNepaliDateAdapter();
        var calendar   = new CalendarService(adapter);
        var conversion = new ConversionService(adapter);
        var loc        = new LocalizationService(TestPaths.DefaultLocalizationPath);

        var vm = new MainViewModel(
            new FakeSettingsService(),
            calendar, loc, conversion,
            new FakeThemeService(),
            new FakeAutoStartService());

        Assert.Null(vm.More.Compression);
        Assert.Null(vm.More.Resize);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static LocalizationService MakeLoc()
    {
        var loc = new LocalizationService(TestPaths.DefaultLocalizationPath);
        loc.SetLanguage("en");
        return loc;
    }

    // Minimal fakes needed to construct MainViewModel without WPF runtime.
    // ThemeService.Apply() touches Application.Current.Resources; FakeThemeService
    // is a no-op, mirroring the pattern in MainViewModelTests.

    private sealed class FakeSettingsService : ISettingsService
    {
        public WidgetSettings Current { get; } = new();
        public bool IsFirstLaunch => false;
        public void Load()  { }
        public void Save()  { }
        public void ResetToDefaults() { }
        public event EventHandler? SettingsChanged;
    }

    private sealed class FakeThemeService : IThemeService
    {
        public string CurrentTheme   { get; private set; } = "Dark";
        public string CurrentPreset  { get; private set; } = "Default";
        public void Apply(string theme, string preset) { CurrentTheme = theme; CurrentPreset = preset; }
        public void OverrideHighlightColor(string colorHex) { }
    }
}
