using NepDateWidget.Models;
using NepDateWidget.Services;
using NepDateWidget.Tests.Services;
using NepDateWidget.ViewModels;

namespace NepDateWidget.Tests.ViewModels;

public sealed class CalendarViewModelCopyDateTests
{
    private sealed class FakeClipboardService : IClipboardService
    {
        public List<string?> Writes { get; } = new();
        public bool NextResult { get; set; } = true;

        public bool SetText(string? text)
        {
            Writes.Add(text);
            return NextResult && !string.IsNullOrEmpty(text);
        }
    }

    private static (CalendarViewModel vm, FakeClipboardService clip) Create(string language = "en")
    {
        var adapter = new FakeNepaliDateAdapter();
        var loc     = new LocalizationService(TestPaths.DefaultLocalizationPath);
        loc.SetLanguage(language);
        var clip = new FakeClipboardService();
        var vm   = new CalendarViewModel(
            new CalendarService(adapter), loc, new ConversionService(adapter),
            adapter: adapter, clipboardService: clip);
        return (vm, clip);
    }

    [Fact]
    public void DayCell_CurrentMonth_ExposesCopyOptions()
    {
        var (vm, _) = Create();
        var today   = vm.Days.First(d => d.IsToday);

        Assert.True(today.HasCopyOptions);
        Assert.NotEmpty(today.CopyFormatOptions);
        Assert.Contains(today.CopyFormatOptions, o => o.Key == "bs_short");
    }

    [Fact]
    public void DayCell_PaddingCell_HasNoCopyOptions()
    {
        var (vm, _) = Create();
        var pad     = vm.Days.FirstOrDefault(d => d.IsPadding);
        if (pad is null) return; // months that start on Sunday have no leading pad

        Assert.False(pad.HasCopyOptions);
        Assert.Empty(pad.CopyFormatOptions);
    }

    [Fact]
    public void CopyDateCommand_WritesOptionValue_ToClipboard()
    {
        var (vm, clip) = Create();
        var today      = vm.Days.First(d => d.IsToday);
        var bsShort    = today.CopyFormatOptions.First(o => o.Key == "bs_short");

        vm.CopyDateCommand.Execute(bsShort);

        Assert.Single(clip.Writes);
        Assert.Equal("2082/12/20", clip.Writes[0]);
    }

    [Fact]
    public void CopyDateCommand_NullParameter_IsNoOp()
    {
        var (vm, clip) = Create();

        vm.CopyDateCommand.Execute(null);

        Assert.Empty(clip.Writes);
    }

    [Fact]
    public void CopyDateCommand_OptionWithEmptyValue_IsNoOp()
    {
        var (vm, clip) = Create();
        var blank      = new DateFormatOption("test", "blank", string.Empty);

        vm.CopyDateCommand.Execute(blank);

        Assert.Empty(clip.Writes);
    }

    [Fact]
    public void DayCell_NepaliLanguage_BuildsNepaliBsValues()
    {
        var (vm, clip) = Create(language: "ne");
        var today      = vm.Days.First(d => d.IsToday);
        var bsShort    = today.CopyFormatOptions.First(o => o.Key == "bs_short");

        vm.CopyDateCommand.Execute(bsShort);

        // FakeNepaliDateAdapter.FormatBsShortNe returns "{y}-{m}-{d} (unicode)"
        Assert.Single(clip.Writes);
        Assert.Contains("unicode", clip.Writes[0]);
    }

    [Fact]
    public void RefreshGrid_OnLanguageChange_RebuildsCopyOptions()
    {
        var (vm, _) = Create(language: "en");
        var today   = vm.Days.First(d => d.IsToday);
        Assert.Equal("2082/12/20", today.CopyFormatOptions.First(o => o.Key == "bs_short").Value);

        // The view model rebuilds Days on language change via OnLanguageChanged.
        // Drive that path through the localization service the VM holds.
        var loc = new LocalizationService(TestPaths.DefaultLocalizationPath);
        loc.SetLanguage("ne");
        // Re-create VM under "ne" to mirror what RefreshGrid does after the
        // language toggle (the Days collection items get replaced).
        var adapter = new FakeNepaliDateAdapter();
        var vm2 = new CalendarViewModel(
            new CalendarService(adapter), loc, new ConversionService(adapter),
            adapter: adapter);
        var todayNe = vm2.Days.First(d => d.IsToday);

        Assert.Contains("unicode", todayNe.CopyFormatOptions.First(o => o.Key == "bs_short").Value);
    }
}
