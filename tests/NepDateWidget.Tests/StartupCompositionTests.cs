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
        var orchestrator = new JobOrchestrationService(image, pdf, new ImageConversionService(), new PdfTranscodeService());
        Assert.NotNull(orchestrator);
    }

    // ── ViewModel construction ────────────────────────────────────────────────

    [Fact]
    public void MoreViewModel_WithImageServices_ExposesNonNullImageTools()
    {
        var loc          = MakeLoc();
        var fileType     = new FileTypeService();
        var image        = new ImageCompressionService();
        var pdf          = new PdfCompressionService();
        var orchestrator = new JobOrchestrationService(image, pdf, new ImageConversionService(), new PdfTranscodeService());
        var imgConv      = new ImageConversionService();

        var vm = new MoreViewModel(loc, fileTypeService: fileType, jobOrchestrationService: orchestrator, imageConversionService: imgConv);

        Assert.NotNull(vm.ImageTools);
    }

    [Fact]
    public void MoreViewModel_WithoutImageServices_LeavesImageToolsNull()
    {
        var vm = new MoreViewModel(MakeLoc());
        Assert.Null(vm.ImageTools);
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
    public void NavigateToImageTools_FromGrid_SetsSubView()
    {
        var loc          = MakeLoc();
        var fileType     = new FileTypeService();
        var image        = new ImageCompressionService();
        var pdf          = new PdfCompressionService();
        var orchestrator = new JobOrchestrationService(image, pdf, new ImageConversionService(), new PdfTranscodeService());
        var imgConv      = new ImageConversionService();
        var vm           = new MoreViewModel(loc, fileTypeService: fileType, jobOrchestrationService: orchestrator, imageConversionService: imgConv);

        Assert.True(vm.IsGridVisible);

        vm.NavigateToCommand.Execute("ImageTools");

        Assert.False(vm.IsGridVisible);
        Assert.Equal("ImageTools", vm.CurrentSubView);
        Assert.True(vm.IsSubViewImageTools);
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

    [Fact]
    public void MainViewModel_WithImageServices_WiresThemToMore()
    {
        var adapter      = new FakeNepaliDateAdapter();
        var calendar     = new CalendarService(adapter);
        var conversion   = new ConversionService(adapter);
        var loc          = new LocalizationService(TestPaths.DefaultLocalizationPath);
        var fileType     = new FileTypeService();
        var image        = new ImageCompressionService();
        var pdf          = new PdfCompressionService();
        var orchestrator = new JobOrchestrationService(image, pdf, new ImageConversionService(), new PdfTranscodeService());
        var imgConv      = new ImageConversionService();

        var vm = new MainViewModel(
            new FakeSettingsService(),
            calendar, loc, conversion,
            new FakeThemeService(),
            new FakeAutoStartService(),
            fileTypeService: fileType,
            jobOrchestrationService: orchestrator,
            imageConversionService: imgConv);

        Assert.NotNull(vm.More.ImageTools);
    }

    [Fact]
    public void MainViewModel_WithoutImageServices_LeavesImageToolsNull()
    {
        var adapter    = new FakeNepaliDateAdapter();
        var calendar   = new CalendarService(adapter);
        var conversion = new ConversionService(adapter);
        var loc        = new LocalizationService(TestPaths.DefaultLocalizationPath);

        var vm = new MainViewModel(
            new FakeSettingsService(),
            calendar, loc, conversion,
            new FakeThemeService(),
            new FakeAutoStartService());

        Assert.Null(vm.More.ImageTools);
    }

    [Fact]
    public void ImageConversionService_CanBeConstructed()
    {
        var svc = new ImageConversionService();
        Assert.NotNull(svc);
    }

    [Fact]
    public void MainViewModel_WithImageConversionService_WiresItToMore()
    {
        var adapter      = new FakeNepaliDateAdapter();
        var calendar     = new CalendarService(adapter);
        var conversion   = new ConversionService(adapter);
        var loc          = new LocalizationService(TestPaths.DefaultLocalizationPath);
        var fileType     = new FileTypeService();
        var image        = new ImageCompressionService();
        var pdf          = new PdfCompressionService();
        var orchestrator = new JobOrchestrationService(image, pdf, new ImageConversionService(), new PdfTranscodeService());
        var imgConv      = new ImageConversionService();

        var vm = new MainViewModel(
            new FakeSettingsService(),
            calendar, loc, conversion,
            new FakeThemeService(),
            new FakeAutoStartService(),
            fileTypeService: fileType,
            jobOrchestrationService: orchestrator,
            imageConversionService: imgConv);

        Assert.NotNull(vm.More.ImageTools);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static LocalizationService MakeLoc()
    {
        var loc = new LocalizationService(TestPaths.DefaultLocalizationPath);
        loc.SetLanguage("en");
        return loc;
    }

    [Fact]
    public void QrCodeViewModel_CanBeConstructed()
    {
        var vm = new QrCodeViewModel(MakeLoc());
        Assert.NotNull(vm);
        Assert.True(vm.IsTypeTextUrl);
        Assert.False(vm.IsTypeWifi);
        Assert.False(vm.IsTypeVCard);
    }

    [Fact]
    public void MoreViewModel_ExposesNonNullQrCode()
    {
        var vm = new MoreViewModel(MakeLoc());
        Assert.NotNull(vm.QrCode);
    }

    [Fact]
    public void NavigateToQrCode_FromGrid_SetsSubView()
    {
        var vm = new MoreViewModel(MakeLoc());
        Assert.True(vm.IsGridVisible);

        vm.NavigateToCommand.Execute("QrCode");

        Assert.False(vm.IsGridVisible);
        Assert.Equal("QrCode", vm.CurrentSubView);
        Assert.True(vm.IsSubViewQrCode);
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
    }

    private sealed class FakeThemeService : IThemeService
    {
        public string CurrentTheme   { get; private set; } = "Dark";
        public string CurrentPreset  { get; private set; } = "Default";
        public void Apply(string theme, string preset) { CurrentTheme = theme; CurrentPreset = preset; }
        public void OverrideHighlightColor(string colorHex) { }
    }
}

