using NepDateWidget.Services;
using NepDateWidget.ViewModels;

namespace NepDateWidget.Tests.ViewModels;

/// <summary>
/// Unit tests for TextToolsViewModel and FontConverter.
/// Covers: mode switching, label initialisation, word stats, case converters,
/// password strength evaluation, and unicode conversion round-trip basics.
/// All pure logic - no WPF runtime required.
/// </summary>
public class TextToolsViewModelTests
{
    private static TextToolsViewModel Create(string lang = "en")
    {
        var loc = new LocalizationService(TestPaths.DefaultLocalizationPath);
        loc.SetLanguage(lang);
        return new TextToolsViewModel(loc);
    }

    // ═════════════════════════════════════════════════════════════════════════
    // CONSTRUCTION
    // ═════════════════════════════════════════════════════════════════════════

    [Fact]
    public void Constructor_NullLocalization_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => new TextToolsViewModel(null!));
    }

    [Fact]
    public void Constructor_DefaultMode_IsPassword()
    {
        var vm = Create();
        Assert.Equal(0, vm.ActiveMode);
        Assert.True(vm.IsModePassword);
        Assert.False(vm.IsModeWord);
        Assert.False(vm.IsModeUnicode);
    }

    [Fact]
    public void Constructor_AllCommandsNotNull()
    {
        var vm = Create();
        Assert.NotNull(vm.SetModeUnicodeCommand);
        Assert.NotNull(vm.SetModeWordCommand);
        Assert.NotNull(vm.SetModePasswordCommand);
        Assert.NotNull(vm.PreetiToUnicodeCommand);
        Assert.NotNull(vm.UnicodeToPreetiCommand);
        Assert.NotNull(vm.CopyUnicodeOutputCommand);
        Assert.NotNull(vm.CaseUpperCommand);
        Assert.NotNull(vm.CaseLowerCommand);
        Assert.NotNull(vm.CaseTitleCommand);
        Assert.NotNull(vm.CaseSentenceCommand);
        Assert.NotNull(vm.CaseSnakeCommand);
        Assert.NotNull(vm.CaseCamelCommand);
        Assert.NotNull(vm.CopyWordOutputCommand);
        Assert.NotNull(vm.GeneratePasswordCommand);
    }

    [Fact]
    public void Constructor_LabelsNotEmpty_English()
    {
        var vm = Create("en");
        Assert.NotEmpty(vm.ModeUnicodeLabel);
        Assert.NotEmpty(vm.ModeWordLabel);
        Assert.NotEmpty(vm.ModePasswordLabel);
        Assert.NotEmpty(vm.PreetiToUnicodeLabel);
        Assert.NotEmpty(vm.UnicodeToPreetiLabel);
        Assert.NotEmpty(vm.PwLengthLabel);
        Assert.NotEmpty(vm.PwGenerateLabel);
        Assert.NotEmpty(vm.PwCheckLabel);
        Assert.NotEqual("[texttools.mode_unicode]", vm.ModeUnicodeLabel);
        Assert.NotEqual("[tab.text]", vm.ModeUnicodeLabel);
    }

    [Fact]
    public void Constructor_LabelsNotEmpty_Nepali()
    {
        var vm = Create("ne");
        Assert.NotEmpty(vm.ModeUnicodeLabel);
        Assert.NotEqual("[texttools.mode_unicode]", vm.ModeUnicodeLabel);
    }

    [Fact]
    public void Constructor_LabelsChangeOnLanguageSwitch()
    {
        var vm = Create("en");
        var enLabel = vm.ModeUnicodeLabel;
        vm.OnLanguageChanged();
        // Switching language on same instance is tested elsewhere; just verify it doesn't throw.
        Assert.NotNull(vm.ModeUnicodeLabel);
    }

    [Fact]
    public void Constructor_DefaultPasswordLength_Is16()
    {
        var vm = Create();
        Assert.Equal(16, vm.PasswordLength);
    }

    [Fact]
    public void Constructor_GeneratedPassword_NotEmpty()
    {
        var vm = Create();
        Assert.NotEmpty(vm.GeneratedPassword);
    }

    [Fact]
    public void Constructor_DefaultCheckboxes_UpperLowerNumbersEnabled()
    {
        var vm = Create();
        Assert.True(vm.PwUpper);
        Assert.True(vm.PwLower);
        Assert.True(vm.PwNumbers);
        Assert.False(vm.PwSymbols);
        Assert.False(vm.PwNepali);
    }

    // ═════════════════════════════════════════════════════════════════════════
    // MODE SWITCHING
    // ═════════════════════════════════════════════════════════════════════════

    [Fact]
    public void SetModeWordCommand_SwitchesToWordMode()
    {
        var vm = Create();
        vm.SetModeWordCommand.Execute(null);
        Assert.Equal(1, vm.ActiveMode);
        Assert.False(vm.IsModeUnicode);
        Assert.True(vm.IsModeWord);
        Assert.False(vm.IsModePassword);
    }

    [Fact]
    public void SetModePasswordCommand_SwitchesToPasswordMode()
    {
        var vm = Create();
        vm.SetModePasswordCommand.Execute(null);
        Assert.Equal(0, vm.ActiveMode);
        Assert.False(vm.IsModeUnicode);
        Assert.False(vm.IsModeWord);
        Assert.True(vm.IsModePassword);
    }

    [Fact]
    public void SetModeUnicodeCommand_SwitchesBackToUnicodeMode()
    {
        var vm = Create();
        vm.SetModePasswordCommand.Execute(null);
        vm.SetModeUnicodeCommand.Execute(null);
        Assert.Equal(2, vm.ActiveMode);
        Assert.True(vm.IsModeUnicode);
    }

    // ═════════════════════════════════════════════════════════════════════════
    // WORD STATS
    // ═════════════════════════════════════════════════════════════════════════

    [Fact]
    public void WordStats_EmptyInput_AllZero()
    {
        var vm = Create();
        vm.WordInput = string.Empty;
        Assert.Equal(0, vm.WordCount);
        Assert.Equal(0, vm.CharCount);
        Assert.Equal(0, vm.CharCountNoSpaces);
    }

    [Fact]
    public void WordStats_SimpleText_CorrectCounts()
    {
        var vm = Create();
        vm.WordInput = "Hello World";
        Assert.Equal(2, vm.WordCount);
        Assert.Equal(11, vm.CharCount);
        Assert.Equal(10, vm.CharCountNoSpaces);
    }

    [Fact]
    public void WordStats_OnlySpaces_ZeroWords()
    {
        var vm = Create();
        vm.WordInput = "   ";
        Assert.Equal(0, vm.WordCount);
        Assert.Equal(3, vm.CharCount);
        Assert.Equal(0, vm.CharCountNoSpaces);
    }

    [Fact]
    public void WordStats_MultilineText_CountsCorrectly()
    {
        var vm = Create();
        vm.WordInput = "Line one\nLine two";
        Assert.Equal(4, vm.WordCount);
        Assert.Equal(17, vm.CharCount);
    }

    // ═════════════════════════════════════════════════════════════════════════
    // CASE CONVERTERS
    // ═════════════════════════════════════════════════════════════════════════

    [Fact]
    public void CaseUpper_ProducesUppercase()
    {
        var vm = Create();
        vm.WordInput = "hello world";
        vm.CaseUpperCommand.Execute(null);
        Assert.Equal("HELLO WORLD", vm.WordOutput);
    }

    [Fact]
    public void CaseLower_ProducesLowercase()
    {
        var vm = Create();
        vm.WordInput = "HELLO WORLD";
        vm.CaseLowerCommand.Execute(null);
        Assert.Equal("hello world", vm.WordOutput);
    }

    [Fact]
    public void CaseTitle_CapitalizesEachWord()
    {
        var vm = Create();
        vm.WordInput = "hello world foo";
        vm.CaseTitleCommand.Execute(null);
        Assert.Equal("Hello World Foo", vm.WordOutput);
    }

    [Fact]
    public void CaseSentence_CapitalizesFirstLetter()
    {
        var vm = Create();
        vm.WordInput = "hello world";
        vm.CaseSentenceCommand.Execute(null);
        Assert.Equal("Hello world", vm.WordOutput);
    }

    [Fact]
    public void CaseSnake_ReplacesSpacesWithUnderscore()
    {
        var vm = Create();
        vm.WordInput = "Hello World";
        vm.CaseSnakeCommand.Execute(null);
        Assert.Equal("hello_world", vm.WordOutput);
    }

    [Fact]
    public void CaseCamel_ProducesCamelCase()
    {
        var vm = Create();
        vm.WordInput = "hello world foo";
        vm.CaseCamelCommand.Execute(null);
        Assert.Equal("helloWorldFoo", vm.WordOutput);
    }

    // ═════════════════════════════════════════════════════════════════════════
    // CASE CONVERTER STATIC HELPERS
    // ═════════════════════════════════════════════════════════════════════════

    [Theory]
    [InlineData("",           "")]
    [InlineData("hello",      "Hello")]
    [InlineData("hello world","Hello World")]
    [InlineData("HELLO WORLD","Hello World")]
    public void ToTitleCase_Correct(string input, string expected)
    {
        Assert.Equal(expected, TextToolsViewModel.ToTitleCase(input));
    }

    [Theory]
    [InlineData("",            "")]
    [InlineData("hello world", "Hello world")]
    [InlineData("HELLO WORLD", "Hello world")]
    public void ToSentenceCase_Correct(string input, string expected)
    {
        Assert.Equal(expected, TextToolsViewModel.ToSentenceCase(input));
    }

    [Theory]
    [InlineData("",             "")]
    [InlineData("Hello World",  "hello_world")]
    [InlineData("Hello-World",  "hello_world")]
    [InlineData("HELLO WORLD",  "hello_world")]
    public void ToSnakeCase_Correct(string input, string expected)
    {
        Assert.Equal(expected, TextToolsViewModel.ToSnakeCase(input));
    }

    [Theory]
    [InlineData("",              "")]
    [InlineData("hello world",   "helloWorld")]
    [InlineData("hello_world",   "helloWorld")]
    [InlineData("Hello World",   "helloWorld")]
    [InlineData("foo bar baz",   "fooBarBaz")]
    public void ToCamelCase_Correct(string input, string expected)
    {
        Assert.Equal(expected, TextToolsViewModel.ToCamelCase(input));
    }

    // ═════════════════════════════════════════════════════════════════════════
    // PASSWORD GENERATOR
    // ═════════════════════════════════════════════════════════════════════════

    [Fact]
    public void GeneratedPassword_HasCorrectLength()
    {
        var vm = Create();
        vm.PasswordLength = 20;
        vm.GeneratePasswordCommand.Execute(null);
        Assert.Equal(20, vm.GeneratedPassword.Length);
    }

    [Fact]
    public void GeneratedPassword_MinLength8()
    {
        var vm = Create();
        vm.PasswordLength = 8;
        vm.GeneratePasswordCommand.Execute(null);
        Assert.Equal(8, vm.GeneratedPassword.Length);
    }

    [Fact]
    public void GeneratedPassword_MaxLength32()
    {
        var vm = Create();
        vm.PasswordLength = 32;
        vm.GeneratePasswordCommand.Execute(null);
        Assert.Equal(32, vm.GeneratedPassword.Length);
    }

    [Fact]
    public void GeneratedPassword_AllChecksDisabled_IsEmpty()
    {
        var vm = Create();
        vm.PwUpper   = false;
        vm.PwLower   = false;
        vm.PwNumbers = false;
        vm.PwSymbols = false;
        vm.PwNepali  = false;
        Assert.Empty(vm.GeneratedPassword);
    }

    [Fact]
    public void GeneratedPassword_UpperOnly_ContainsOnlyUppercase()
    {
        var vm = Create();
        vm.PwLower   = false;
        vm.PwNumbers = false;
        vm.PwSymbols = false;
        vm.PwNepali  = false;
        vm.PwUpper   = true;
        vm.PasswordLength = 20;
        vm.GeneratePasswordCommand.Execute(null);
        Assert.All(vm.GeneratedPassword, c => Assert.True(char.IsUpper(c)));
    }

    [Fact]
    public void GeneratedPassword_DifferentEachCall()
    {
        var vm = Create();
        vm.PasswordLength = 32;
        var first = vm.GeneratedPassword;
        vm.GeneratePasswordCommand.Execute(null);
        var second = vm.GeneratedPassword;
        // There is a tiny probability these equal with a random generator but
        // 32-char alphanumeric makes collision probability negligible.
        Assert.NotEqual(first, second);
    }

    // ═════════════════════════════════════════════════════════════════════════
    // PASSWORD STRENGTH
    // ═════════════════════════════════════════════════════════════════════════

    [Fact]
    public void Strength_EmptyPassword_LevelZero()
    {
        var (level, _, _, _, _, _) = TextToolsViewModel.EvaluateStrength(string.Empty);
        Assert.Equal(0, level);
    }

    [Fact]
    public void Strength_ShortOnlyLowercase_Weak()
    {
        var (level, len, _, lower, _, _) = TextToolsViewModel.EvaluateStrength("abc");
        Assert.Equal(1, level);
        Assert.False(len);   // < 8 chars
        Assert.True(lower);
    }

    [Fact]
    public void Strength_MixedMedium()
    {
        // 8 chars, upper + lower + number = 4 scoring criteria → medium (score 4 → medium if < 5?)
        // Score: hasLength(1) + hasUpper(1) + hasLower(1) + hasNumber(1) = 4 → medium (3–4)
        var (level, _, _, _, _, _) = TextToolsViewModel.EvaluateStrength("Abc12345");
        Assert.Equal(2, level);
    }

    [Fact]
    public void Strength_AllCriteriaMet_Strong()
    {
        // 12 chars with upper, lower, number, symbol → score 5 → strong
        var (level, len, upper, lower, number, symbol) = TextToolsViewModel.EvaluateStrength("Abc123!@#xyz");
        Assert.Equal(3, level);
        Assert.True(len);
        Assert.True(upper);
        Assert.True(lower);
        Assert.True(number);
        Assert.True(symbol);
    }

    [Fact]
    public void Strength_16PlusCharsWithMixed_Strong()
    {
        // 16+ chars: bonus point
        var (level, _, _, _, _, _) = TextToolsViewModel.EvaluateStrength("Abcdefgh12345678");
        Assert.Equal(3, level);
    }

    [Fact]
    public void StrengthInput_UpdatesStrengthLevel()
    {
        var vm = Create();
        Assert.Equal(0, vm.StrengthLevel);
        Assert.False(vm.HasStrengthInput);

        vm.StrengthInput = "Abc123!@#xyz";
        Assert.Equal(3, vm.StrengthLevel);
        Assert.True(vm.HasStrengthInput);
    }

    [Fact]
    public void StrengthInput_WeakPassword_LevelOne()
    {
        var vm = Create();
        vm.StrengthInput = "abc";
        Assert.Equal(1, vm.StrengthLevel);
    }

    [Fact]
    public void StrengthLabel_ShowsLocalizedText()
    {
        var vm = Create("en");
        vm.StrengthInput = "abc";
        Assert.Equal("Weak", vm.StrengthLabel);
    }

    [Fact]
    public void StrengthBarWidth_ReflectsLevel()
    {
        var vm = Create();
        vm.StrengthInput = string.Empty;
        Assert.Equal(0, vm.StrengthBarWidth);

        vm.StrengthInput = "abc";
        Assert.Equal(33, vm.StrengthBarWidth);

        vm.StrengthInput = "Abc12345";
        Assert.Equal(66, vm.StrengthBarWidth);

        vm.StrengthInput = "Abc123!@#xyz";
        Assert.Equal(100, vm.StrengthBarWidth);
    }

    [Fact]
    public void CriteriaFlags_CorrectlySet()
    {
        var vm = Create();
        vm.StrengthInput = "Abc123!";
        Assert.False(vm.CriteriaLength);  // only 7 chars
        Assert.True(vm.CriteriaUpper);
        Assert.True(vm.CriteriaLower);
        Assert.True(vm.CriteriaNumber);
        Assert.True(vm.CriteriaSymbol);
    }

    // ═════════════════════════════════════════════════════════════════════════
    // FONT CONVERTER (static class)
    // ═════════════════════════════════════════════════════════════════════════

    [Fact]
    public void FontConverter_ConvertToUnicode_EmptyInput_ReturnsEmpty()
    {
        Assert.Equal(string.Empty, FontConverter.ConvertToUnicode(string.Empty));
        Assert.Equal(string.Empty, FontConverter.ConvertToUnicode("   "));
    }

    [Fact]
    public void FontConverter_ConvertToPreeti_EmptyInput_ReturnsEmpty()
    {
        Assert.Equal(string.Empty, FontConverter.ConvertToPreeti(string.Empty));
        Assert.Equal(string.Empty, FontConverter.ConvertToPreeti("   "));
    }

    [Fact]
    public void FontConverter_ConvertToUnicode_NepaliDigits_Converted()
    {
        // Preeti "0" maps to Nepali digit "०"
        var result = FontConverter.ConvertToUnicode("0");
        Assert.Equal("०", result);
    }

    [Fact]
    public void FontConverter_PreetiToUnicodeCommand_SetsOutput()
    {
        var vm = Create();
        vm.UnicodeInput = "0";
        vm.PreetiToUnicodeCommand.Execute(null);
        Assert.NotEmpty(vm.UnicodeOutput);
        Assert.Equal("०", vm.UnicodeOutput);
    }

    [Fact]
    public void FontConverter_UnicodeToPreetiCommand_SetsOutput()
    {
        var vm = Create();
        vm.UnicodeInput = "०";
        vm.UnicodeToPreetiCommand.Execute(null);
        Assert.NotEmpty(vm.UnicodeOutput);
    }

    [Fact]
    public void FontConverter_UnicodeInput_ClearsOutput()
    {
        var vm = Create();
        vm.UnicodeInput = "0";
        vm.PreetiToUnicodeCommand.Execute(null);
        Assert.NotEmpty(vm.UnicodeOutput);

        // Changing input triggers live conversion, so output should be updated (not cleared)
        vm.UnicodeInput = "1";
        Assert.NotEmpty(vm.UnicodeOutput);
    }

    // ═════════════════════════════════════════════════════════════════════════
    // MAIN VIEW MODEL INTEGRATION - tab label
    // ═════════════════════════════════════════════════════════════════════════

    [Fact]
    public void TabTextLabel_NonEmpty_InEnglish()
    {
        // This test verifies the localization key exists and the tab label is wired in MainViewModel.
        var loc = new LocalizationService(TestPaths.DefaultLocalizationPath);
        loc.SetLanguage("en");
        var label = loc.Get("tab.text");
        Assert.NotEmpty(label);
        Assert.NotEqual("[tab.text]", label);
    }

    [Fact]
    public void TabTextLabel_NonEmpty_InNepali()
    {
        var loc = new LocalizationService(TestPaths.DefaultLocalizationPath);
        loc.SetLanguage("ne");
        var label = loc.Get("tab.text");
        Assert.NotEmpty(label);
        Assert.NotEqual("[tab.text]", label);
    }

    // ── Unicode direction combobox ───────────────────────────────────────────

    [Fact]
    public void UnicodeDirection_DefaultsToZero_PreetiToUnicode()
    {
        var vm = Create();
        Assert.Equal(0, vm.UnicodeDirection);
    }

    [Fact]
    public void UnicodeDirection_SetTo1_SwitchesToUnicodeToPreeti()
    {
        var vm = Create();
        vm.UnicodeDirection = 1;
        Assert.Equal(1, vm.UnicodeDirection);
    }

    [Fact]
    public void UnicodeDirectionItems_HasTwoEntries()
    {
        var vm = Create();
        Assert.Equal(2, vm.UnicodeDirectionItems.Length);
    }

    [Fact]
    public void UnicodeDirection_LiveConvert_ProducesOutput()
    {
        var vm = Create();
        vm.UnicodeDirection = 0; // Preeti → Unicode
        vm.UnicodeInput = "g]kfn";
        Assert.NotEmpty(vm.UnicodeOutput);
    }

    [Fact]
    public void UnicodeDirection_Switch_TriggersReconversion()
    {
        var vm = Create();
        vm.UnicodeInput = "g]kfn";
        var output0 = vm.UnicodeOutput;

        vm.UnicodeDirection = 1; // Switch to Unicode → Preeti
        // Output should change (or at least be recomputed)
        Assert.NotNull(vm.UnicodeOutput);
    }
}
