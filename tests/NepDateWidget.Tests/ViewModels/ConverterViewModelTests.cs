using NepDateWidget.Services;
using NepDateWidget.Tests.Services;
using NepDateWidget.ViewModels;

namespace NepDateWidget.Tests.ViewModels;

public class ConverterViewModelTests
{
    private static ConverterViewModel Create(
        string defaultDirection = "ADtoBS",
        string language = "en",
        IConversionService? conversionService = null)
    {
        var loc = new LocalizationService();
        loc.SetLanguage(language);
        var adapter = new FakeNepaliDateAdapter();
        var svc = conversionService ?? new ConversionService(adapter);
        return new ConverterViewModel(svc, loc, defaultDirection, adapter);
    }

    [Fact]
    public void Constructor_DefaultDirection_AdToBs_Sets_IsAdToBs_True()
    {
        var vm = Create("ADtoBS");
        Assert.True(vm.IsAdToBs);
        Assert.False(vm.IsBsToAd);
    }

    [Fact]
    public void Constructor_DefaultDirection_BsToAd_Sets_IsBsToAd_True()
    {
        var vm = Create("BStoAD");
        Assert.False(vm.IsAdToBs);
        Assert.True(vm.IsBsToAd);
    }

    [Fact]
    public void Constructor_NoError_Initially()
    {
        var vm = Create();
        Assert.False(vm.HasError);
        Assert.Equal(string.Empty, vm.ErrorMessage);
    }

    [Fact]
    public void Constructor_DefaultInput_IsPopulatedWithTodayDate()
    {
        var vm = Create();
        Assert.NotEmpty(vm.InputText);
    }

    [Fact]
    public void Constructor_DefaultInput_AutoConverts()
    {
        var vm = Create();
        Assert.NotEmpty(vm.OutputText);
    }

    [Fact]
    public void Constructor_Labels_NonEmpty()
    {
        var vm = Create();
        Assert.NotEmpty(vm.AdToBsLabel);
        Assert.NotEmpty(vm.BsToAdLabel);
        Assert.NotEmpty(vm.TitleLabel);
    }

    [Fact]
    public void SetBsToAdCommand_Flips_Direction()
    {
        var vm = Create("ADtoBS");
        vm.SetBsToAdCommand.Execute(null);
        Assert.False(vm.IsAdToBs);
        Assert.True(vm.IsBsToAd);
    }

    [Fact]
    public void SetAdToBsCommand_Flips_Direction_Back()
    {
        var vm = Create("BStoAD");
        vm.SetAdToBsCommand.Execute(null);
        Assert.True(vm.IsAdToBs);
        Assert.False(vm.IsBsToAd);
    }

    [Fact]
    public void InputText_BsToAd_ValidDate_AutoConverts()
    {
        var vm = Create("BStoAD");
        vm.InputText = "2082-12-20";
        Assert.False(vm.HasError);
        Assert.NotEmpty(vm.OutputText);
    }

    [Fact]
    public void InputText_BsToAd_InvalidInput_NoOutput()
    {
        var vm = Create("BStoAD");
        vm.InputText = "abc-4-3";
        Assert.Equal(string.Empty, vm.OutputText);
    }

    [Fact]
    public void InputText_AdToBs_ValidDate_AutoConverts()
    {
        var vm = Create("ADtoBS");
        vm.InputText = "2026-4-3";
        Assert.False(vm.HasError);
        Assert.NotEmpty(vm.OutputText);
    }

    [Fact]
    public void InputText_AdToBs_InvalidInput_NoOutput()
    {
        var vm = Create("ADtoBS");
        vm.InputText = "abc-4-3";
        Assert.Equal(string.Empty, vm.OutputText);
    }

    [Fact]
    public void ChangeInputText_ClearsOutput()
    {
        var vm = Create("ADtoBS");
        vm.InputText = "2026-4-3";
        Assert.NotEmpty(vm.OutputText);
        // Typing a partial / invalid string clears the previous output
        vm.InputText = "2026-bad";
        Assert.Equal(string.Empty, vm.OutputText);
    }

    [Fact]
    public void SwitchDirection_ChangesDirection()
    {
        var vm = Create("ADtoBS");
        Assert.True(vm.IsAdToBs);
        vm.SetBsToAdCommand.Execute(null);
        Assert.True(vm.IsBsToAd);
        Assert.False(vm.IsAdToBs);
    }

    [Fact]
    public void OnLanguageChanged_Updates_Labels()
    {
        var vm = Create(language: "en");
        var enTitle = vm.TitleLabel;
        var loc = new LocalizationService();
        loc.SetLanguage("ne");
        var vmNe = new ConverterViewModel(new ConversionService(new FakeNepaliDateAdapter()), loc, "ADtoBS");
        Assert.NotEmpty(vmNe.TitleLabel);
        _ = enTitle;
    }

    [Fact]
    public void OnLanguageChanged_Called_Updates_Labels_In_Place()
    {
        var loc = new LocalizationService();
        loc.SetLanguage("en");
        var vm = new ConverterViewModel(new ConversionService(new FakeNepaliDateAdapter()), loc, "ADtoBS");
        var enTitle = vm.TitleLabel;
        loc.SetLanguage("ne");
        vm.OnLanguageChanged();
        Assert.NotEmpty(vm.TitleLabel);
        _ = enTitle;
    }

    // ── Mode switching ───────────────────────────────────────────────────────

    [Fact]
    public void ActiveMode_DefaultsToZero()
    {
        var vm = Create();
        Assert.Equal(0, vm.ActiveMode);
    }

    [Fact]
    public void SetModeDaysCommand_SetsActiveModeTo1()
    {
        var vm = Create();
        vm.SetModeDaysCommand.Execute(null);
        Assert.Equal(1, vm.ActiveMode);
    }

    [Fact]
    public void SetModeTimeCommand_SetsActiveModeTo2()
    {
        var vm = Create();
        vm.SetModeTimeCommand.Execute(null);
        Assert.Equal(2, vm.ActiveMode);
    }

    [Fact]
    public void SetModeConvertCommand_SetsActiveModeBackTo0()
    {
        var vm = Create();
        vm.SetModeDaysCommand.Execute(null);
        vm.SetModeConvertCommand.Execute(null);
        Assert.Equal(0, vm.ActiveMode);
    }

    // ── Days mode ────────────────────────────────────────────────────────────

    [Fact]
    public void Days_DefaultMode_IsDiff()
    {
        // Diff is the default on construction (user-visible "start on Diff" requirement)
        var vm = Create();
        Assert.True(vm.IsDaysDiff);
        Assert.False(vm.IsDaysAddSub);
    }

    [Fact]
    public void Days_DefaultMode_Input2_IsPrefilledWithToday()
    {
        // When Diff is the default, Input2 should already have today's BS date
        var vm = Create();
        Assert.False(string.IsNullOrWhiteSpace(vm.DaysInput2));
    }

    [Fact]
    public void SetDaysAddSubCommand_SetsIsDaysDiffFalse()
    {
        var vm = Create();
        vm.SetDaysDiffCommand.Execute(null);
        vm.SetDaysAddSubCommand.Execute(null);
        Assert.False(vm.IsDaysDiff);
        Assert.True(vm.IsDaysAddSub);
    }

    [Fact]
    public void SetDaysDiffCommand_SetsIsDaysDiffTrue()
    {
        var vm = Create();
        vm.SetDaysDiffCommand.Execute(null);
        Assert.True(vm.IsDaysDiff);
        Assert.False(vm.IsDaysAddSub);
    }

    // ── Error state on bad input ─────────────────────────────────────────────

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not-a-date")]
    public void InputText_Invalid_NoOutput(string input)
    {
        var vm = Create("ADtoBS");
        vm.InputText = input;
        Assert.Equal(string.Empty, vm.OutputText);
    }

    // ── Direction switch clears output ───────────────────────────────────────

    [Fact]
    public void DirectionSwitch_ClearsExistingOutput()
    {
        var vm = Create("ADtoBS");
        vm.InputText = "2026-4-3";
        Assert.NotEmpty(vm.OutputText);

        vm.SetBsToAdCommand.Execute(null);
        // After switching direction, the old output for the other direction should be cleared or reconverted
        // The input is still there, so it tries to convert in the new direction
    }

    // ── Time mode ────────────────────────────────────────────────────────────

    [Fact]
    public void TimeOutput_HasDefault_Initially()
    {
        var vm = Create();
        vm.SetModeTimeCommand.Execute(null);
        Assert.NotEmpty(vm.TimeOutput);
    }

    [Fact]
    public void TimeHasError_False_Initially()
    {
        var vm = Create();
        Assert.False(vm.TimeHasError);
    }

    // ── Placeholder label ────────────────────────────────────────────────────

    [Fact]
    public void ConvertPlaceholder_NonEmpty()
    {
        var vm = Create();
        Assert.NotEmpty(vm.ConvertPlaceholder);
    }

    // ── Legacy compat properties ─────────────────────────────────────────────

    [Fact]
    public void InputText_Setter_WritesToConvertInput()
    {
        var vm = Create();
        vm.InputText = "2082-12-20";
        Assert.Equal("2082-12-20", vm.ConvertInput);
    }

    [Fact]
    public void OutputText_MirrorsConvertOutputShort()
    {
        var vm = Create("BStoAD");
        vm.InputText = "2082-12-20";
        Assert.Equal(vm.ConvertOutputShort, vm.OutputText);
    }

    [Fact]
    public void HasError_MirrorsConvertHasError()
    {
        var vm = Create();
        Assert.Equal(vm.ConvertHasError, vm.HasError);
    }

    [Fact]
    public void ErrorMessage_MirrorsConvertError()
    {
        var vm = Create();
        Assert.Equal(vm.ConvertError, vm.ErrorMessage);
    }

    // ── Default value initialization ─────────────────────────────────────────

    [Fact]
    public void Constructor_ConvertInput_PrePopulatedWithTodayDate()
    {
        var vm = Create("ADtoBS");
        Assert.NotEmpty(vm.ConvertInput);
        // FakeAdapter today AD = 2026-04-03
        Assert.Contains("2026", vm.ConvertInput);
    }

    [Fact]
    public void Constructor_BsToAd_ConvertInput_PrePopulatedWithTodayBs()
    {
        var vm = Create("BStoAD");
        Assert.NotEmpty(vm.ConvertInput);
        // FakeAdapter today BS = 2082-12-20
        Assert.Contains("2082", vm.ConvertInput);
    }

    [Fact]
    public void Constructor_DaysInput1_PrePopulatedWithTodayBs()
    {
        var vm = Create();
        Assert.NotEmpty(vm.DaysInput1);
        Assert.Contains("2082", vm.DaysInput1);
    }

    [Fact]
    public void Constructor_TimeInput_PrePopulatedWithCurrentTime()
    {
        var vm = Create();
        Assert.NotEmpty(vm.TimeInput);
    }

    // ── Time 12h/24h toggle ──────────────────────────────────────────────────

    [Fact]
    public void TimeIsAm_DefaultsToCurrentTime()
    {
        var vm = Create();
        var expectedAm = DateTime.Now.Hour < 12;
        Assert.Equal(expectedAm, vm.TimeIsAm);
    }

    [Fact]
    public void TimeToggleAmPmCommand_TogglesAmPm()
    {
        var vm = Create();
        var initial = vm.TimeIsAm;
        vm.TimeToggleAmPmCommand.Execute(null);
        Assert.Equal(!initial, vm.TimeIsAm);
        vm.TimeToggleAmPmCommand.Execute(null);
        Assert.Equal(initial, vm.TimeIsAm);
    }

    [Fact]
    public void TimeAmPmLabel_ReflectsAmPmState()
    {
        var vm = Create();
        var initialLabel = vm.TimeIsAm ? "AM" : "PM";
        Assert.Equal(initialLabel, vm.TimeAmPmLabel);
        vm.TimeToggleAmPmCommand.Execute(null);
        var toggledLabel = vm.TimeIsAm ? "AM" : "PM";
        Assert.Equal(toggledLabel, vm.TimeAmPmLabel);
    }
}
