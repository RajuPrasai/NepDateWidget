using NepDateWidget.Helpers;
using NepDateWidget.Services;
using NepDateWidget.ViewModels;

namespace NepDateWidget.Tests.ViewModels;

public class UnitViewModelTests
{
    private static UnitViewModel Create(string lang = "en")
    {
        var loc = new LocalizationService(TestPaths.DefaultLocalizationPath);
        loc.SetLanguage(lang);
        return new UnitViewModel(loc);
    }

    // ═════════════════════════════════════════════════════════════════════════
    // CONSTRUCTION / LABELS
    // ═════════════════════════════════════════════════════════════════════════

    [Fact]
    public void Constructor_DefaultMode_IsArea()
    {
        var vm = Create();
        Assert.True(vm.IsModeArea);
        Assert.False(vm.IsModeScript);
        Assert.False(vm.IsModeWeight);
    }

    [Fact]
    public void Constructor_Labels_NonEmpty()
    {
        var vm = Create();
        Assert.NotEmpty(vm.ModeLabelArea);
        Assert.NotEmpty(vm.ModeLabelScript);
        Assert.NotEmpty(vm.ModeLabelWeight);
        Assert.NotEmpty(vm.AreaFromLabel);
        Assert.NotEmpty(vm.AreaToLabel);
        Assert.NotEmpty(vm.WeightFromLabel);
        Assert.NotEmpty(vm.WeightToLabel);
        Assert.NotEmpty(vm.ScriptHintLabel);
    }

    [Fact]
    public void OnLanguageChanged_Ne_UpdatesLabels()
    {
        var loc = new LocalizationService(TestPaths.DefaultLocalizationPath);
        loc.SetLanguage("en");
        var vm = new UnitViewModel(loc);
        var enLabel = vm.ModeLabelArea;

        loc.SetLanguage("ne");
        vm.OnLanguageChanged();

        Assert.NotEqual(enLabel, vm.ModeLabelArea);
        Assert.NotEmpty(vm.ModeLabelArea);
    }

    // ═════════════════════════════════════════════════════════════════════════
    // MODE SWITCHING
    // ═════════════════════════════════════════════════════════════════════════

    [Fact]
    public void SetModeScriptCommand_SwitchesToScript()
    {
        var vm = Create();
        vm.SetModeScriptCommand.Execute(null);
        Assert.False(vm.IsModeArea);
        Assert.True(vm.IsModeScript);
        Assert.False(vm.IsModeWeight);
    }

    [Fact]
    public void SetModeWeightCommand_SwitchesToWeight()
    {
        var vm = Create();
        vm.SetModeWeightCommand.Execute(null);
        Assert.False(vm.IsModeArea);
        Assert.False(vm.IsModeScript);
        Assert.True(vm.IsModeWeight);
    }

    [Fact]
    public void SetModeAreaCommand_SwitchesBack()
    {
        var vm = Create();
        vm.SetModeWeightCommand.Execute(null);
        vm.SetModeAreaCommand.Execute(null);
        Assert.True(vm.IsModeArea);
    }

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)]
    public void ActiveMode_SetDirectly_UpdatesBooleans(int mode)
    {
        var vm = Create();
        vm.ActiveMode = mode;
        Assert.Equal(mode == 0, vm.IsModeArea);
        Assert.Equal(mode == 1, vm.IsModeScript);
        Assert.Equal(mode == 2, vm.IsModeWeight);
    }

    // ═════════════════════════════════════════════════════════════════════════
    // AREA CONVERTER
    // ═════════════════════════════════════════════════════════════════════════

    [Fact]
    public void Area_EmptyInput_NoResult()
    {
        var vm = Create();
        vm.AreaFromValue = "";
        Assert.Equal(string.Empty, vm.AreaResult);
        Assert.False(vm.AreaHasError);
    }

    [Fact]
    public void Area_ZeroInput_ReturnsZero()
    {
        var vm = Create();
        vm.AreaFromUnit = 1; // Bigha
        vm.AreaToUnit   = 9; // Sq. Metres
        vm.AreaFromValue = "0";
        Assert.Equal("0", vm.AreaResult);
        Assert.False(vm.AreaHasError);
    }

    [Fact]
    public void Area_NonNumeric_SetsError()
    {
        var vm = Create();
        vm.AreaFromValue = "abc";
        Assert.True(vm.AreaHasError);
        Assert.NotEmpty(vm.AreaError);
        Assert.Equal(string.Empty, vm.AreaResult);
    }

    [Fact]
    public void Area_Negative_SetsError()
    {
        var vm = Create();
        vm.AreaFromValue = "-5";
        Assert.True(vm.AreaHasError);
    }

    [Fact]
    public void Area_RopanitToSqMetres_KnownValue()
    {
        // 1 Ropani = 508.80 sq m  (index 3 → index 9)
        var vm = Create();
        vm.AreaFromUnit  = 3; // Ropani
        vm.AreaToUnit    = 9; // Sq. Metres
        vm.AreaFromValue = "1";
        Assert.False(vm.AreaHasError);
        Assert.Equal("508.8", vm.AreaResult);
    }

    [Fact]
    public void Area_SqMetresToRopani_RoundTrip()
    {
        // 508.72 sq m → 1 Ropani → back to 508.72 sq m
        var vm = Create();
        vm.AreaFromUnit  = 9; // Sq. Metres
        vm.AreaToUnit    = 3; // Ropani
        vm.AreaFromValue = "508.72";
        Assert.False(vm.AreaHasError);
        double result = double.Parse(vm.AreaResult, System.Globalization.CultureInfo.InvariantCulture);
        Assert.InRange(result, 0.9999, 1.0001);
    }

    [Fact]
    public void Area_SameUnit_ReturnsIdenticalValue()
    {
        var vm = Create();
        vm.AreaFromUnit  = 1; // Kattha
        vm.AreaToUnit    = 1; // Kattha
        vm.AreaFromValue = "3.5";
        Assert.Equal("3.5", vm.AreaResult);
    }

    [Fact]
    public void Area_ChangeUnit_RecalculatesResult()
    {
        var vm = Create();
        vm.AreaFromUnit  = 0; // Bigha
        vm.AreaToUnit    = 9; // Sq. Metres
        vm.AreaFromValue = "1";
        var first = vm.AreaResult;
        vm.AreaToUnit = 7; // Sq. Feet
        var second = vm.AreaResult;
        Assert.NotEqual(first, second);
    }

    // ═════════════════════════════════════════════════════════════════════════
    // NEPALI SCRIPT CONVERTER (NepaliScriptConverter)
    // ═════════════════════════════════════════════════════════════════════════

    [Theory]
    [InlineData("yo",       "यो")]
    [InlineData("namaste",  "नमस्ते")]
    [InlineData("nepaal",   "नेपाल")]
    [InlineData("ka",       "क")]    // k + inherent 'a' = क (no visible matra)
    [InlineData("kaa",      "का")]   // k + explicit 'aa' = का (ā matra)
    public void Script_RomanToDevanagari_KnownWords(string roman, string expected)
    {
        var vm = Create();
        vm.ScriptRomanInput = roman;
        Assert.Equal(expected, vm.ScriptDevaOutput);
    }

    [Fact]
    public void Script_EmptyRoman_EmptyDeva()
    {
        var vm = Create();
        vm.ScriptRomanInput = "";
        Assert.Equal(string.Empty, vm.ScriptDevaOutput);
    }

    [Fact]
    public void Script_EmptyDeva_EmptyRoman()
    {
        var vm = Create();
        vm.ScriptDevaInput = "";
        Assert.Equal(string.Empty, vm.ScriptRomanOutput);
    }

    [Fact]
    public void Script_DevaToRoman_ProducesNonEmpty()
    {
        var vm = Create();
        vm.ScriptDevaInput = "नेपाल";
        Assert.NotEmpty(vm.ScriptRomanOutput);
    }

    [Fact]
    public void Script_RomanAndDevaInputsAreIndependent()
    {
        var vm = Create();
        vm.ScriptRomanInput = "namaste";
        vm.ScriptDevaInput  = "नमस्ते";
        Assert.NotEmpty(vm.ScriptDevaOutput);
        Assert.NotEmpty(vm.ScriptRomanOutput);
    }

    // ═════════════════════════════════════════════════════════════════════════
    // WEIGHT / VOLUME CONVERTER
    // ═════════════════════════════════════════════════════════════════════════

    [Fact]
    public void Weight_EmptyInput_NoResult()
    {
        var vm = Create();
        vm.WeightFromValue = "";
        Assert.Equal(string.Empty, vm.WeightResult);
        Assert.False(vm.WeightHasError);
    }

    [Fact]
    public void Weight_NonNumeric_SetsError()
    {
        var vm = Create();
        vm.WeightFromValue = "xyz";
        Assert.True(vm.WeightHasError);
        Assert.Equal(string.Empty, vm.WeightResult);
    }

    [Fact]
    public void Weight_Negative_SetsError()
    {
        var vm = Create();
        vm.WeightFromValue = "-1";
        Assert.True(vm.WeightHasError);
    }

    [Fact]
    public void Weight_DharniToKg_KnownValue()
    {
        // 1 Dharni = 2.3325 kg  (UN/Nepal Standard Weights & Measures Act 1968)
        var vm = Create();
        vm.WeightFromUnit  = 0; // Dharni
        vm.WeightToUnit    = 5; // kg
        vm.WeightFromValue = "1";
        Assert.False(vm.WeightHasError);
        Assert.Equal("2.33", vm.WeightResult);
    }

    [Fact]
    public void Weight_MuriToKg_KnownValue()
    {
        // 1 Muri = 90.9192 kg  (20 Pathi × 4.54596 kg; Standard Weights & Measures Act 1968)
        var vm = Create();
        vm.WeightFromUnit  = 4; // Muri
        vm.WeightToUnit    = 5; // kg
        vm.WeightFromValue = "1";
        Assert.Equal("90.92", vm.WeightResult);
    }

    [Fact]
    public void Weight_KgToGrams_KnownValue()
    {
        // 1 kg = 1000 g  (index 5 → index 6)
        var vm = Create();
        vm.WeightFromUnit  = 5; // kg
        vm.WeightToUnit    = 6; // g
        vm.WeightFromValue = "1";
        Assert.Equal("1000", vm.WeightResult);
    }

    [Fact]
    public void Weight_ZeroInput_ReturnsZero()
    {
        var vm = Create();
        vm.WeightFromUnit  = 0; // Dharni
        vm.WeightToUnit    = 5; // kg
        vm.WeightFromValue = "0";
        Assert.Equal("0", vm.WeightResult);
        Assert.False(vm.WeightHasError);
    }

    [Fact]
    public void Weight_SameUnit_ReturnsSameValue()
    {
        var vm = Create();
        vm.WeightFromUnit  = 3; // Pathi
        vm.WeightToUnit    = 3; // Pathi
        vm.WeightFromValue = "7";
        Assert.Equal("7", vm.WeightResult);
    }

    // ═════════════════════════════════════════════════════════════════════════
    // NepaliScriptConverter static tests
    // ═════════════════════════════════════════════════════════════════════════

    [Fact]
    public void ScriptConverter_RomanToDevanagari_ConsonantCluster()
    {
        // "ksha" → क्ष
        var result = NepaliScriptConverter.RomanToDevanagari("ksha");
        Assert.Equal("क्ष", result);
    }

    [Fact]
    public void ScriptConverter_RomanToDevanagari_Digraph_sh()
    {
        // "sha" → श (sh + inherent a)
        var result = NepaliScriptConverter.RomanToDevanagari("sha");
        Assert.Equal("श", result);
    }

    [Fact]
    public void ScriptConverter_RomanToDevanagari_VowelAa()
    {
        // "aa" standalone → आ
        var result = NepaliScriptConverter.RomanToDevanagari("aa");
        Assert.Equal("आ", result);
    }

    [Fact]
    public void ScriptConverter_RomanToDevanagari_EmptyInput()
    {
        Assert.Equal(string.Empty, NepaliScriptConverter.RomanToDevanagari(""));
        Assert.Equal(string.Empty, NepaliScriptConverter.RomanToDevanagari(null!));
    }

    [Fact]
    public void ScriptConverter_DevanagariToRoman_EmptyInput()
    {
        Assert.Equal(string.Empty, NepaliScriptConverter.DevanagariToRoman(""));
        Assert.Equal(string.Empty, NepaliScriptConverter.DevanagariToRoman(null!));
    }

    [Fact]
    public void ScriptConverter_DevanagariToRoman_SimpleWord()
    {
        // नमस्ते → namaste (or some roman equivalent, just non-empty)
        var result = NepaliScriptConverter.DevanagariToRoman("नमस्ते");
        Assert.NotEmpty(result);
        Assert.DoesNotContain("[", result); // no localization fallback markers
    }

    [Fact]
    public void ScriptConverter_PassthroughDigits_AndSpaces()
    {
        var result = NepaliScriptConverter.RomanToDevanagari("ko 2 cha");
        Assert.Contains("2", result);
        Assert.Contains(" ", result);
    }
}
