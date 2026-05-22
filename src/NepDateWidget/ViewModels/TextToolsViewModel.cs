using NepDateWidget.Helpers;
using NepDateWidget.Services;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Windows.Input;

namespace NepDateWidget.ViewModels;

/// <summary>
/// View model for the Text Tools tab.
/// Four modes: Unicode (0), Word (1), Password (2), Script (3).
/// All logic is pure - no WPF types, no external dependencies.
/// </summary>
public sealed class TextToolsViewModel : ViewModelBase
{
    private readonly ILocalizationService _loc;

    // ─────────────────────────────────────────────────────────────────────────
    // MODE SELECTION  (0 = Password, 1 = Word, 2 = Unicode, 3 = Script)
    // The order matches TextToolsView.xaml's tab-strip column order. The
    // integer mapping is the source of truth - the on-screen first tab
    // must always equal ActiveMode == 0.
    // ─────────────────────────────────────────────────────────────────────────

    private int _activeMode;
    public int ActiveMode
    {
        get => _activeMode;
        set
        {
            if (SetProperty(ref _activeMode, value))
            {
                Log.Action($"texttools mode → {value}");
                OnPropertyChanged(nameof(IsModeUnicode));
                OnPropertyChanged(nameof(IsModeWord));
                OnPropertyChanged(nameof(IsModePassword));
                OnPropertyChanged(nameof(IsModeScript));
            }
        }
    }

    public bool IsModePassword { get => _activeMode == 0; set { if (value) { ActiveMode = 0; } } }
    public bool IsModeWord { get => _activeMode == 1; set { if (value) { ActiveMode = 1; } } }
    public bool IsModeUnicode { get => _activeMode == 2; set { if (value) { ActiveMode = 2; } } }
    public bool IsModeScript { get => _activeMode == 3; set { if (value) { ActiveMode = 3; } } }

    // ─────────────────────────────────────────────────────────────────────────
    // MODE: UNICODE CONVERTER
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>0 = Preeti → Unicode, 1 = Unicode → Preeti</summary>
    private int _unicodeDirection;
    public int UnicodeDirection
    {
        get => _unicodeDirection;
        set
        {
            if (SetProperty(ref _unicodeDirection, value))
            {
                // Sync file conversion direction with inline direction
                _unicodeFilePreetiToUnicode = value == 0;
                OnPropertyChanged(nameof(UnicodeFilePreetiToUnicode));
                OnPropertyChanged(nameof(UnicodeFileUnicodeToPreeti));
                OnPropertyChanged(nameof(IsUnicodeDir0));
                OnPropertyChanged(nameof(IsUnicodeDir1));
                AutoConvertUnicode();
            }
        }
    }
    public bool IsUnicodeDir0 => _unicodeDirection == 0;
    public bool IsUnicodeDir1 => _unicodeDirection == 1;

    private string _unicodeInput = string.Empty;
    public string UnicodeInput
    {
        get => _unicodeInput;
        set
        {
            if (SetProperty(ref _unicodeInput, value))
            {
                AutoConvertUnicode();
            }
        }
    }

    private void AutoConvertUnicode()
    {
        if (string.IsNullOrEmpty(_unicodeInput))
        {
            UnicodeOutput = string.Empty;
            return;
        }
        UnicodeOutput = _unicodeDirection == 0
            ? FontConverter.ConvertToUnicode(_unicodeInput)
            : FontConverter.ConvertToPreeti(_unicodeInput);
    }

    private string _unicodeOutput = string.Empty;
    public string UnicodeOutput
    {
        get => _unicodeOutput;
        private set => SetProperty(ref _unicodeOutput, value);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // MODE: WORD TOOLS
    // ─────────────────────────────────────────────────────────────────────────

    private string _wordInput = string.Empty;
    public string WordInput
    {
        get => _wordInput;
        set
        {
            if (SetProperty(ref _wordInput, value))
            {
                RefreshWordStats();
            }
        }
    }

    private int _wordCount;
    public int WordCount
    {
        get => _wordCount;
        private set => SetProperty(ref _wordCount, value);
    }

    private int _charCount;
    public int CharCount
    {
        get => _charCount;
        private set => SetProperty(ref _charCount, value);
    }

    private int _charCountNoSpaces;
    public int CharCountNoSpaces
    {
        get => _charCountNoSpaces;
        private set => SetProperty(ref _charCountNoSpaces, value);
    }

    private string _wordOutput = string.Empty;
    public string WordOutput
    {
        get => _wordOutput;
        private set => SetProperty(ref _wordOutput, value);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // MODE: NEPALI SCRIPT CONVERTER (live direction toggle)
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>0 = Roman → Devanagari, 1 = Devanagari → Roman</summary>
    private int _scriptDirection;
    public int ScriptDirection
    {
        get => _scriptDirection;
        set
        {
            if (SetProperty(ref _scriptDirection, value))
            {
                // Sync file conversion direction
                _scriptFileRomanToDeva = value == 0;
                OnPropertyChanged(nameof(ScriptFileRomanToDeva));
                OnPropertyChanged(nameof(ScriptFileDevaToRoman));
                OnPropertyChanged(nameof(IsScriptDir0));
                OnPropertyChanged(nameof(IsScriptDir1));
                AutoConvertScript();
            }
        }
    }
    public bool IsScriptDir0 => _scriptDirection == 0;
    public bool IsScriptDir1 => _scriptDirection == 1;

    public string[] ScriptDirectionItems { get; private set; } = Array.Empty<string>();

    private string _scriptBoxA = string.Empty;
    public string ScriptBoxA
    {
        get => _scriptBoxA;
        set
        {
            if (SetProperty(ref _scriptBoxA, value))
            {
                AutoConvertScript();
            }
        }
    }

    private void AutoConvertScript()
    {
        ScriptBoxB = _scriptDirection == 0
            ? NepaliScriptConverter.RomanToDevanagari(_scriptBoxA)
            : NepaliScriptConverter.DevanagariToRoman(_scriptBoxA);
    }

    private string _scriptBoxB = string.Empty;
    public string ScriptBoxB { get => _scriptBoxB; set => SetProperty(ref _scriptBoxB, value); }

    // ─────────────────────────────────────────────────────────────────────────
    // FILE CONVERSION STATE: Unicode panel
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Delegate set by the View to open a Save File dialog.
    /// Receives the suggested default path, returns the chosen path or null if cancelled.
    /// </summary>
    public Func<string, string?>? RequestSaveFilePath { get; set; }

    private string? _unicodeFilePath;
    private bool _unicodeFilePreetiToUnicode = true;
    private bool _unicodeFileIsConverting;
    private string _unicodeFileStatus = string.Empty;
    private bool _unicodeFileHasError;

    public string UnicodeFileName =>
        _unicodeFilePath is null
            ? _loc.Get("texttools.file_no_file")
            : Path.GetFileName(_unicodeFilePath);

    public bool UnicodeFilePreetiToUnicode
    {
        get => _unicodeFilePreetiToUnicode;
        private set
        {
            if (SetProperty(ref _unicodeFilePreetiToUnicode, value))
            {
                OnPropertyChanged(nameof(UnicodeFileUnicodeToPreeti));
            }
        }
    }
    public bool UnicodeFileUnicodeToPreeti => !_unicodeFilePreetiToUnicode;

    public bool UnicodeFileIsConverting
    {
        get => _unicodeFileIsConverting;
        private set
        {
            if (SetProperty(ref _unicodeFileIsConverting, value))
            {
                OnPropertyChanged(nameof(UnicodeFileCanConvert));
            }
        }
    }
    public bool UnicodeFileCanConvert => _unicodeFilePath is not null && !_unicodeFileIsConverting;

    public string UnicodeFileStatus
    {
        get => _unicodeFileStatus;
        private set
        {
            if (SetProperty(ref _unicodeFileStatus, value))
            {
                OnPropertyChanged(nameof(UnicodeFileHasStatus));
            }
        }
    }
    public bool UnicodeFileHasError { get => _unicodeFileHasError; private set => SetProperty(ref _unicodeFileHasError, value); }
    public bool UnicodeFileHasStatus => !string.IsNullOrEmpty(_unicodeFileStatus);

    public void SetUnicodeFilePath(string? path)
    {
        _unicodeFilePath = path;
        UnicodeFileStatus = string.Empty;
        UnicodeFileHasError = false;
        OnPropertyChanged(nameof(UnicodeFileName));
        OnPropertyChanged(nameof(UnicodeFileCanConvert));
    }

    // ─────────────────────────────────────────────────────────────────────────
    // FILE CONVERSION STATE: Script panel
    // ─────────────────────────────────────────────────────────────────────────

    private string? _scriptFilePath;
    private bool _scriptFileRomanToDeva = true;
    private bool _scriptFileIsConverting;
    private string _scriptFileStatus = string.Empty;
    private bool _scriptFileHasError;

    public string ScriptFileName =>
        _scriptFilePath is null
            ? _loc.Get("texttools.file_no_file")
            : Path.GetFileName(_scriptFilePath);

    public bool ScriptFileRomanToDeva
    {
        get => _scriptFileRomanToDeva;
        private set
        {
            if (SetProperty(ref _scriptFileRomanToDeva, value))
            {
                OnPropertyChanged(nameof(ScriptFileDevaToRoman));
            }
        }
    }
    public bool ScriptFileDevaToRoman => !_scriptFileRomanToDeva;

    public bool ScriptFileIsConverting
    {
        get => _scriptFileIsConverting;
        private set
        {
            if (SetProperty(ref _scriptFileIsConverting, value))
            {
                OnPropertyChanged(nameof(ScriptFileCanConvert));
            }
        }
    }
    public bool ScriptFileCanConvert => _scriptFilePath is not null && !_scriptFileIsConverting;

    public string ScriptFileStatus
    {
        get => _scriptFileStatus;
        private set
        {
            if (SetProperty(ref _scriptFileStatus, value))
            {
                OnPropertyChanged(nameof(ScriptFileHasStatus));
            }
        }
    }
    public bool ScriptFileHasError { get => _scriptFileHasError; private set => SetProperty(ref _scriptFileHasError, value); }
    public bool ScriptFileHasStatus => !string.IsNullOrEmpty(_scriptFileStatus);

    public void SetScriptFilePath(string? path)
    {
        _scriptFilePath = path;
        ScriptFileStatus = string.Empty;
        ScriptFileHasError = false;
        OnPropertyChanged(nameof(ScriptFileName));
        OnPropertyChanged(nameof(ScriptFileCanConvert));
    }

    // ─────────────────────────────────────────────────────────────────────────
    // MODE: PASSWORD GENERATOR + STRENGTH CHECKER
    // ─────────────────────────────────────────────────────────────────────────

    private double _passwordLength = 16;
    public double PasswordLength
    {
        get => _passwordLength;
        set
        {
            double rounded = Math.Round(value);
            if (SetProperty(ref _passwordLength, rounded))
            {
                GeneratePassword();
            }
        }
    }

    private bool _pwUpper = true;
    public bool PwUpper
    {
        get => _pwUpper;
        set
        {
            if (SetProperty(ref _pwUpper, value))
            {
                GeneratePassword();
            }
        }
    }

    private bool _pwLower = true;
    public bool PwLower
    {
        get => _pwLower;
        set
        {
            if (SetProperty(ref _pwLower, value))
            {
                GeneratePassword();
            }
        }
    }

    private bool _pwNumbers = true;
    public bool PwNumbers
    {
        get => _pwNumbers;
        set
        {
            if (SetProperty(ref _pwNumbers, value))
            {
                GeneratePassword();
            }
        }
    }

    private bool _pwSymbols = false;
    public bool PwSymbols
    {
        get => _pwSymbols;
        set
        {
            if (SetProperty(ref _pwSymbols, value))
            {
                GeneratePassword();
            }
        }
    }

    private bool _pwNepali = false;
    public bool PwNepali
    {
        get => _pwNepali;
        set
        {
            if (SetProperty(ref _pwNepali, value))
            {
                GeneratePassword();
            }
        }
    }

    private string _generatedPassword = string.Empty;
    public string GeneratedPassword
    {
        get => _generatedPassword;
        private set => SetProperty(ref _generatedPassword, value);
    }

    private string _strengthInput = string.Empty;
    public string StrengthInput
    {
        get => _strengthInput;
        set
        {
            if (SetProperty(ref _strengthInput, value))
            {
                RefreshStrength();
            }
        }
    }

    // 0 = empty/none, 1 = weak, 2 = medium, 3 = strong
    private int _strengthLevel;
    public int StrengthLevel
    {
        get => _strengthLevel;
        private set
        {
            if (SetProperty(ref _strengthLevel, value))
            {
                OnPropertyChanged(nameof(StrengthLabel));
                OnPropertyChanged(nameof(StrengthBarWidth));
                OnPropertyChanged(nameof(HasStrengthInput));
                OnPropertyChanged(nameof(CriteriaLength));
                OnPropertyChanged(nameof(CriteriaUpper));
                OnPropertyChanged(nameof(CriteriaLower));
                OnPropertyChanged(nameof(CriteriaNumber));
                OnPropertyChanged(nameof(CriteriaSymbol));
            }
        }
    }

    public string StrengthLabel => _strengthLevel switch
    {
        1 => _loc.Get("texttools.pw_weak"),
        2 => _loc.Get("texttools.pw_medium"),
        3 => _loc.Get("texttools.pw_strong"),
        _ => string.Empty
    };

    // A value 0-100 that drives a ProgressBar or Width binding.
    public double StrengthBarWidth => _strengthLevel switch { 1 => 33, 2 => 66, 3 => 100, _ => 0 };

    public bool HasStrengthInput => !string.IsNullOrEmpty(_strengthInput);

    // Individual strength criteria (for checklist display)
    public bool CriteriaLength { get; private set; }
    public bool CriteriaUpper { get; private set; }
    public bool CriteriaLower { get; private set; }
    public bool CriteriaNumber { get; private set; }
    public bool CriteriaSymbol { get; private set; }

    // ─────────────────────────────────────────────────────────────────────────
    // LOCALIZED LABELS
    // ─────────────────────────────────────────────────────────────────────────

    public string ModeUnicodeLabel { get; private set; } = string.Empty;
    public string ModeWordLabel { get; private set; } = string.Empty;
    public string ModePasswordLabel { get; private set; } = string.Empty;
    public string ModeScriptLabel { get; private set; } = string.Empty;

    public string UnicodeInputLabel { get; private set; } = string.Empty;
    public string UnicodeOutputLabel { get; private set; } = string.Empty;
    public string PreetiToUnicodeLabel { get; private set; } = string.Empty;
    public string UnicodeToPreetiLabel { get; private set; } = string.Empty;
    public string[] UnicodeDirectionItems { get; private set; } = Array.Empty<string>();
    public string CopyLabel { get; private set; } = string.Empty;

    public string WordInputLabel { get; private set; } = string.Empty;
    public string WordCountLabel { get; private set; } = string.Empty;
    public string CharCountLabel { get; private set; } = string.Empty;
    public string CharNoSpacesLabel { get; private set; } = string.Empty;
    public string CaseUpperLabel { get; private set; } = string.Empty;
    public string CaseLowerLabel { get; private set; } = string.Empty;
    public string CaseTitleLabel { get; private set; } = string.Empty;
    public string CaseSentenceLabel { get; private set; } = string.Empty;
    public string CaseSnakeLabel { get; private set; } = string.Empty;
    public string CaseCamelLabel { get; private set; } = string.Empty;

    public string PwLengthLabel { get; private set; } = string.Empty;
    public string PwUpperLabel { get; private set; } = string.Empty;
    public string PwLowerLabel { get; private set; } = string.Empty;
    public string PwNumbersLabel { get; private set; } = string.Empty;
    public string PwSymbolsLabel { get; private set; } = string.Empty;
    public string PwNepaliLabel { get; private set; } = string.Empty;
    public string PwGenerateLabel { get; private set; } = string.Empty;
    public string PwStrengthLabel { get; private set; } = string.Empty;
    public string PwCheckLabel { get; private set; } = string.Empty;
    public string CriteriaLengthLabel { get; private set; } = string.Empty;
    public string CriteriaUpperLabel { get; private set; } = string.Empty;
    public string CriteriaLowerLabel { get; private set; } = string.Empty;
    public string CriteriaNumberLabel { get; private set; } = string.Empty;
    public string CriteriaSymbolLabel { get; private set; } = string.Empty;

    public string WordOutputLabel { get; private set; } = string.Empty;
    public string ScriptRomanInLabel { get; private set; } = string.Empty;
    public string ScriptDevaOutLabel { get; private set; } = string.Empty;
    public string ScriptDevaInLabel { get; private set; } = string.Empty;
    public string ScriptRomanOutLabel { get; private set; } = string.Empty;
    public string ScriptHintLabel { get; private set; } = string.Empty;
    public string ScriptInputLabel { get; private set; } = string.Empty;
    public string ScriptOutputLabel { get; private set; } = string.Empty;
    public string ScriptRomanToDevaLabel { get; private set; } = string.Empty;
    public string ScriptDevaToRomanLabel { get; private set; } = string.Empty;

    // File section labels (shared between Unicode and Script file sections)
    public string FileSectionLabel { get; private set; } = string.Empty;
    public string FileBrowseLabel { get; private set; } = string.Empty;
    public string FileConvertLabel { get; private set; } = string.Empty;
    public string FileConvertingLabel { get; private set; } = string.Empty;

    // ─────────────────────────────────────────────────────────────────────────
    // COMMANDS
    // ─────────────────────────────────────────────────────────────────────────

    // Mode switch commands
    public ICommand SetModeUnicodeCommand { get; }
    public ICommand SetModeWordCommand { get; }
    public ICommand SetModePasswordCommand { get; }
    public ICommand SetModeScriptCommand { get; }
    public ICommand ScriptRomanToDevaCommand { get; }
    public ICommand ScriptDevaToRomanCommand { get; }

    public ICommand SetUnicodeDir0Command { get; }
    public ICommand SetUnicodeDir1Command { get; }
    public ICommand SetScriptDir0Command { get; }
    public ICommand SetScriptDir1Command { get; }

    public ICommand PreetiToUnicodeCommand { get; }
    public ICommand UnicodeToPreetiCommand { get; }
    public ICommand CopyUnicodeOutputCommand { get; }

    public ICommand CaseUpperCommand { get; }
    public ICommand CaseLowerCommand { get; }
    public ICommand CaseTitleCommand { get; }
    public ICommand CaseSentenceCommand { get; }
    public ICommand CaseSnakeCommand { get; }
    public ICommand CaseCamelCommand { get; }
    public ICommand CopyWordOutputCommand { get; }

    public ICommand GeneratePasswordCommand { get; }

    // ── FILE CONVERSION: Unicode ──────────────────────────────────────────────
    // These drive the "Convert a File" section at the bottom of the Unicode panel.

    public ICommand SetUnicodeFileDirP2UCommand { get; }
    public ICommand SetUnicodeFileDirU2PCommand { get; }
    public ICommand ConvertUnicodeFileCommand { get; }

    // ── FILE CONVERSION: Script ───────────────────────────────────────────────

    public ICommand SetScriptFileDirR2DCommand { get; }
    public ICommand SetScriptFileDirD2RCommand { get; }
    public ICommand ConvertScriptFileCommand { get; }
    public ICommand OpenHelpCommand { get; }

    // ─────────────────────────────────────────────────────────────────────────
    // CONSTRUCTION
    // ─────────────────────────────────────────────────────────────────────────

    public TextToolsViewModel(ILocalizationService loc)
    {
        _loc = loc ?? throw new ArgumentNullException(nameof(loc));

        SetModePasswordCommand = new RelayCommand(() => ActiveMode = 0);
        SetModeWordCommand = new RelayCommand(() => ActiveMode = 1);
        SetModeUnicodeCommand = new RelayCommand(() => ActiveMode = 2);
        SetModeScriptCommand = new RelayCommand(() => ActiveMode = 3);
        ScriptRomanToDevaCommand = new RelayCommand(() => ScriptBoxB = NepaliScriptConverter.RomanToDevanagari(_scriptBoxA));
        ScriptDevaToRomanCommand = new RelayCommand(() => ScriptBoxB = NepaliScriptConverter.DevanagariToRoman(_scriptBoxA));

        SetUnicodeDir0Command = new RelayCommand(() => UnicodeDirection = 0);
        SetUnicodeDir1Command = new RelayCommand(() => UnicodeDirection = 1);
        SetScriptDir0Command = new RelayCommand(() => ScriptDirection = 0);
        SetScriptDir1Command = new RelayCommand(() => ScriptDirection = 1);

        PreetiToUnicodeCommand = new RelayCommand(() => UnicodeOutput = FontConverter.ConvertToUnicode(_unicodeInput));
        UnicodeToPreetiCommand = new RelayCommand(() => UnicodeOutput = FontConverter.ConvertToPreeti(_unicodeInput));
        CopyUnicodeOutputCommand = new RelayCommand(() => SafeClipboardSet(_unicodeOutput));

        CaseUpperCommand = new RelayCommand(() => WordOutput = _wordInput.ToUpperInvariant());
        CaseLowerCommand = new RelayCommand(() => WordOutput = _wordInput.ToLowerInvariant());
        CaseTitleCommand = new RelayCommand(() => WordOutput = ToTitleCase(_wordInput));
        CaseSentenceCommand = new RelayCommand(() => WordOutput = ToSentenceCase(_wordInput));
        CaseSnakeCommand = new RelayCommand(() => WordOutput = ToSnakeCase(_wordInput));
        CaseCamelCommand = new RelayCommand(() => WordOutput = ToCamelCase(_wordInput));
        CopyWordOutputCommand = new RelayCommand(() => SafeClipboardSet(_wordOutput));

        GeneratePasswordCommand = new RelayCommand(GeneratePassword);

        SetUnicodeFileDirP2UCommand = new RelayCommand(() => UnicodeFilePreetiToUnicode = true);
        SetUnicodeFileDirU2PCommand = new RelayCommand(() => UnicodeFilePreetiToUnicode = false);
        ConvertUnicodeFileCommand = new RelayCommand(
            async () => await ConvertUnicodeFileAsync(),
            () => UnicodeFileCanConvert);

        SetScriptFileDirR2DCommand = new RelayCommand(() => ScriptFileRomanToDeva = true);
        SetScriptFileDirD2RCommand = new RelayCommand(() => ScriptFileRomanToDeva = false);
        ConvertScriptFileCommand = new RelayCommand(
            async () => await ConvertScriptFileAsync(),
            () => ScriptFileCanConvert);
        OpenHelpCommand = new RelayCommand<string>(key =>
        {
            var shell = System.Windows.Application.Current.Windows
                .OfType<NepDateWidget.Views.ExpandedShellWindow>()
                .FirstOrDefault(w => w.IsVisible)
                ?? (System.Windows.Window)System.Windows.Application.Current.MainWindow!;
            NepDateWidget.Views.HelpPopup.ShowFor(key!, _loc, shell);
        });

        RefreshLabels();
        GeneratePassword();
    }

    // ─────────────────────────────────────────────────────────────────────────
    // LANGUAGE SUPPORT
    // ─────────────────────────────────────────────────────────────────────────

    public void OnLanguageChanged()
    {
        RefreshLabels();
        OnPropertyChanged(nameof(StrengthLabel));
    }

    private void RefreshLabels()
    {
        ModeUnicodeLabel = _loc.Get("texttools.mode_unicode");
        ModeWordLabel = _loc.Get("texttools.mode_word");
        ModePasswordLabel = _loc.Get("texttools.mode_password");
        ModeScriptLabel = _loc.Get("texttools.mode_script");

        UnicodeInputLabel = _loc.Get("texttools.unicode_input");
        UnicodeOutputLabel = _loc.Get("texttools.unicode_output");
        PreetiToUnicodeLabel = _loc.Get("texttools.preeti_to_unicode");
        UnicodeToPreetiLabel = _loc.Get("texttools.unicode_to_preeti");
        UnicodeDirectionItems = new[] { PreetiToUnicodeLabel, UnicodeToPreetiLabel };
        ScriptDirectionItems = new[] { ScriptRomanToDevaLabel, ScriptDevaToRomanLabel };
        CopyLabel = _loc.Get("texttools.copy");

        WordInputLabel = _loc.Get("texttools.word_input");
        WordCountLabel = _loc.Get("texttools.word_count");
        CharCountLabel = _loc.Get("texttools.char_count");
        CharNoSpacesLabel = _loc.Get("texttools.char_no_spaces");
        CaseUpperLabel = _loc.Get("texttools.case_upper");
        CaseLowerLabel = _loc.Get("texttools.case_lower");
        CaseTitleLabel = _loc.Get("texttools.case_title");
        CaseSentenceLabel = _loc.Get("texttools.case_sentence");
        CaseSnakeLabel = _loc.Get("texttools.case_snake");
        CaseCamelLabel = _loc.Get("texttools.case_camel");

        PwLengthLabel = _loc.Get("texttools.pw_length");
        PwUpperLabel = _loc.Get("texttools.pw_upper");
        PwLowerLabel = _loc.Get("texttools.pw_lower");
        PwNumbersLabel = _loc.Get("texttools.pw_numbers");
        PwSymbolsLabel = _loc.Get("texttools.pw_symbols");
        PwNepaliLabel = _loc.Get("texttools.pw_nepali");
        PwGenerateLabel = _loc.Get("texttools.pw_generate");
        PwStrengthLabel = _loc.Get("texttools.pw_strength");
        PwCheckLabel = _loc.Get("texttools.pw_check");
        CriteriaLengthLabel = _loc.Get("texttools.criteria_length");
        CriteriaUpperLabel = _loc.Get("texttools.criteria_upper");
        CriteriaLowerLabel = _loc.Get("texttools.criteria_lower");
        CriteriaNumberLabel = _loc.Get("texttools.criteria_number");
        CriteriaSymbolLabel = _loc.Get("texttools.criteria_symbol");

        WordOutputLabel = _loc.Get("texttools.word_output");
        ScriptRomanInLabel = _loc.Get("unit.script.roman_in");
        ScriptDevaOutLabel = _loc.Get("unit.script.deva_out");
        ScriptDevaInLabel = _loc.Get("unit.script.deva_in");
        ScriptRomanOutLabel = _loc.Get("unit.script.roman_out");
        ScriptHintLabel = _loc.Get("unit.script.hint");
        ScriptInputLabel = _loc.Get("texttools.script_input");
        ScriptOutputLabel = _loc.Get("texttools.script_output");
        ScriptRomanToDevaLabel = _loc.Get("texttools.script_r2d");
        ScriptDevaToRomanLabel = _loc.Get("texttools.script_d2r");

        FileSectionLabel = _loc.Get("texttools.file_section");
        FileBrowseLabel = _loc.Get("texttools.file_browse");
        FileConvertLabel = _loc.Get("texttools.file_convert");
        FileConvertingLabel = _loc.Get("texttools.file_converting");

        OnPropertyChanged(nameof(ModeUnicodeLabel));
        OnPropertyChanged(nameof(ModeWordLabel));
        OnPropertyChanged(nameof(ModePasswordLabel));
        OnPropertyChanged(nameof(UnicodeInputLabel));
        OnPropertyChanged(nameof(UnicodeOutputLabel));
        OnPropertyChanged(nameof(PreetiToUnicodeLabel));
        OnPropertyChanged(nameof(UnicodeToPreetiLabel));
        OnPropertyChanged(nameof(UnicodeDirectionItems));
        OnPropertyChanged(nameof(CopyLabel));
        OnPropertyChanged(nameof(WordInputLabel));
        OnPropertyChanged(nameof(WordCountLabel));
        OnPropertyChanged(nameof(CharCountLabel));
        OnPropertyChanged(nameof(CharNoSpacesLabel));
        OnPropertyChanged(nameof(CaseUpperLabel));
        OnPropertyChanged(nameof(CaseLowerLabel));
        OnPropertyChanged(nameof(CaseTitleLabel));
        OnPropertyChanged(nameof(CaseSentenceLabel));
        OnPropertyChanged(nameof(CaseSnakeLabel));
        OnPropertyChanged(nameof(CaseCamelLabel));
        OnPropertyChanged(nameof(PwLengthLabel));
        OnPropertyChanged(nameof(PwUpperLabel));
        OnPropertyChanged(nameof(PwLowerLabel));
        OnPropertyChanged(nameof(PwNumbersLabel));
        OnPropertyChanged(nameof(PwSymbolsLabel));
        OnPropertyChanged(nameof(PwNepaliLabel));
        OnPropertyChanged(nameof(PwGenerateLabel));
        OnPropertyChanged(nameof(PwStrengthLabel));
        OnPropertyChanged(nameof(PwCheckLabel));
        OnPropertyChanged(nameof(CriteriaLengthLabel));
        OnPropertyChanged(nameof(CriteriaUpperLabel));
        OnPropertyChanged(nameof(CriteriaLowerLabel));
        OnPropertyChanged(nameof(CriteriaNumberLabel));
        OnPropertyChanged(nameof(CriteriaSymbolLabel));
        OnPropertyChanged(nameof(WordOutputLabel));
        OnPropertyChanged(nameof(ModeScriptLabel));
        OnPropertyChanged(nameof(ScriptRomanInLabel));
        OnPropertyChanged(nameof(ScriptDevaOutLabel));
        OnPropertyChanged(nameof(ScriptDevaInLabel));
        OnPropertyChanged(nameof(ScriptRomanOutLabel));
        OnPropertyChanged(nameof(ScriptHintLabel));
        OnPropertyChanged(nameof(ScriptInputLabel));
        OnPropertyChanged(nameof(ScriptOutputLabel));
        OnPropertyChanged(nameof(ScriptRomanToDevaLabel));
        OnPropertyChanged(nameof(ScriptDevaToRomanLabel));
        OnPropertyChanged(nameof(ScriptDirectionItems));
        OnPropertyChanged(nameof(FileSectionLabel));
        OnPropertyChanged(nameof(FileBrowseLabel));
        OnPropertyChanged(nameof(FileConvertLabel));
        OnPropertyChanged(nameof(FileConvertingLabel));
        // Refresh file name placeholder text (may contain a localized string)
        OnPropertyChanged(nameof(UnicodeFileName));
        OnPropertyChanged(nameof(ScriptFileName));
    }

    // ─────────────────────────────────────────────────────────────────────────
    // WORD STATS
    // ─────────────────────────────────────────────────────────────────────────

    private void RefreshWordStats()
    {
        var text = _wordInput;
        CharCount = text.Length;
        CharCountNoSpaces = text.Replace(" ", string.Empty).Replace("\t", string.Empty)
                                .Replace("\r", string.Empty).Replace("\n", string.Empty).Length;

        if (string.IsNullOrWhiteSpace(text))
        {
            WordCount = 0;
        }
        else
        {
            WordCount = text.Split(new[] { ' ', '\t', '\r', '\n' },
                StringSplitOptions.RemoveEmptyEntries).Length;
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // CASE CONVERTERS
    // ─────────────────────────────────────────────────────────────────────────

    internal static string ToTitleCase(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return text;
        }

        var words = text.Split(' ');
        for (int i = 0; i < words.Length; i++)
        {
            var w = words[i];
            if (w.Length == 0)
            {
                continue;
            }

            words[i] = char.ToUpperInvariant(w[0]) + w.Substring(1).ToLowerInvariant();
        }
        return string.Join(" ", words);
    }

    internal static string ToSentenceCase(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return text;
        }

        var lower = text.ToLowerInvariant();
        return char.ToUpperInvariant(lower[0]) + lower.Substring(1);
    }

    internal static string ToSnakeCase(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return text;
        }

        return text.Trim()
                   .ToLowerInvariant()
                   .Replace(' ', '_')
                   .Replace('-', '_');
    }

    internal static string ToCamelCase(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return text;
        }

        var words = text.Split(new[] { ' ', '_', '-' }, StringSplitOptions.RemoveEmptyEntries);
        if (words.Length == 0)
        {
            return text;
        }

        var sb = new StringBuilder();
        sb.Append(words[0].ToLowerInvariant());
        for (int i = 1; i < words.Length; i++)
        {
            var w = words[i];
            sb.Append(char.ToUpperInvariant(w[0]));
            if (w.Length > 1)
            {
                sb.Append(w.Substring(1).ToLowerInvariant());
            }
        }
        return sb.ToString();
    }

    // ─────────────────────────────────────────────────────────────────────────
    // PASSWORD GENERATOR
    // ─────────────────────────────────────────────────────────────────────────

    private const string UpperChars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
    private const string LowerChars = "abcdefghijklmnopqrstuvwxyz";
    private const string NumberChars = "0123456789";
    private const string SymbolChars = "!@#$%^&*()-_=+[]{}|;:,.<>?";
    private const string NepaliChars = "कखगघङचछजझञटठडढणतथदधनपफबभमयरलवशषसह";

    private void GeneratePassword()
    {
        // Build list of enabled charsets so we can guarantee at least one from each.
        var charsets = new System.Collections.Generic.List<string>();
        if (_pwUpper)
        {
            charsets.Add(UpperChars);
        }

        if (_pwLower)
        {
            charsets.Add(LowerChars);
        }

        if (_pwNumbers)
        {
            charsets.Add(NumberChars);
        }

        if (_pwSymbols)
        {
            charsets.Add(SymbolChars);
        }

        if (_pwNepali)
        {
            charsets.Add(NepaliChars);
        }

        if (charsets.Count == 0)
        {
            GeneratedPassword = string.Empty;
            return;
        }

        var pool = string.Concat(charsets);
        var result = new char[(int)_passwordLength];

        // Place one guaranteed character from each enabled charset first,
        // up to the requested length.  Remaining positions draw from the full pool.
        int pos = 0;
        foreach (var charset in charsets)
        {
            if (pos >= (int)_passwordLength)
            {
                break;
            }

            result[pos++] = charset[RandomNumberGenerator.GetInt32(charset.Length)];
        }
        for (; pos < (int)_passwordLength; pos++)
        {
            result[pos] = pool[RandomNumberGenerator.GetInt32(pool.Length)];
        }

        // Fisher-Yates shuffle so the guaranteed chars are not always at the front.
        for (int j = result.Length - 1; j > 0; j--)
        {
            int k = RandomNumberGenerator.GetInt32(j + 1);
            (result[j], result[k]) = (result[k], result[j]);
        }

        GeneratedPassword = new string(result);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // PASSWORD STRENGTH CHECKER
    // ─────────────────────────────────────────────────────────────────────────

    internal static (int level, bool hasLength, bool hasUpper, bool hasLower, bool hasNumber, bool hasSymbol)
        EvaluateStrength(string password)
    {
        if (string.IsNullOrEmpty(password))
        {
            return (0, false, false, false, false, false);
        }

        bool hasLength = password.Length >= 8;
        bool hasUpper = false;
        bool hasLower = false;
        bool hasNumber = false;
        bool hasSymbol = false;

        foreach (char c in password)
        {
            if (char.IsUpper(c))
            {
                hasUpper = true;
            }

            if (char.IsLower(c))
            {
                hasLower = true;
            }

            if (char.IsDigit(c))
            {
                hasNumber = true;
            }

            if (!char.IsLetterOrDigit(c))
            {
                hasSymbol = true;
            }
        }

        int score = (hasLength ? 1 : 0)
                  + (hasUpper ? 1 : 0)
                  + (hasLower ? 1 : 0)
                  + (hasNumber ? 1 : 0)
                  + (hasSymbol ? 1 : 0);

        // Bonus: very long passwords (16+) get one extra point
        if (password.Length >= 16)
        {
            score++;
        }

        int level = score switch
        {
            >= 5 => 3,
            >= 3 => 2,
            _ => 1
        };

        return (level, hasLength, hasUpper, hasLower, hasNumber, hasSymbol);
    }

    private void RefreshStrength()
    {
        var (level, len, upper, lower, number, symbol) = EvaluateStrength(_strengthInput);

        CriteriaLength = len;
        CriteriaUpper = upper;
        CriteriaLower = lower;
        CriteriaNumber = number;
        CriteriaSymbol = symbol;

        StrengthLevel = level;

        OnPropertyChanged(nameof(CriteriaLength));
        OnPropertyChanged(nameof(CriteriaUpper));
        OnPropertyChanged(nameof(CriteriaLower));
        OnPropertyChanged(nameof(CriteriaNumber));
        OnPropertyChanged(nameof(CriteriaSymbol));
    }

    // ─────────────────────────────────────────────────────────────────────────
    // FILE CONVERSION LOGIC
    // ─────────────────────────────────────────────────────────────────────────

    private async Task ConvertUnicodeFileAsync()
    {
        if (_unicodeFilePath is null)
        {
            return;
        }

        UnicodeFileIsConverting = true;
        UnicodeFileStatus = string.Empty;
        UnicodeFileHasError = false;
        try
        {
            var inputPath = _unicodeFilePath;
            bool p2u = _unicodeFilePreetiToUnicode;

            // P2U: only convert runs whose font is a known legacy Nepali font,
            //      OR where font is null (e.g. plain .txt - convert everything).
            // U2P: convert all runs regardless of font.
            Func<string, string?, string> fn = p2u
                ? (text, font) => font is null || DocumentConverterService.IsLegacyNepaliFont(font)
                                    ? FontConverter.ConvertToUnicode(text) : text
                : (text, _) => FontConverter.ConvertToPreeti(text);

            // After text is converted, update the run font so the document renders correctly:
            // P2U: remove the explicit legacy font - Word will apply Unicode/Devanagari fallback.
            // U2P: set "Preeti" so Preeti-encoded characters display through the Preeti glyph table.
            Func<string?, string?> fontMapper = p2u
                ? (_ => string.Empty)
                : (_ => "Preeti");

            var defaultPath = DocumentConverterService.BuildOutputPath(inputPath);
            var outputPath = RequestSaveFilePath?.Invoke(defaultPath);
            if (outputPath is null)
            {
                UnicodeFileIsConverting = false;
                return;
            }
            await Task.Run(() => DocumentConverterService.Convert(inputPath, outputPath, fn, fontMapper));

            UnicodeFileStatus = _loc.Get("texttools.file_saved") + " " + Path.GetFileName(outputPath);
            UnicodeFileHasError = false;
        }
        catch (Exception ex)
        {
            Log.Error($"unicode file convert: {ex.Message}");
            UnicodeFileStatus = ex.Message;
            UnicodeFileHasError = true;
        }
        finally
        {
            UnicodeFileIsConverting = false;
        }
    }

    private async Task ConvertScriptFileAsync()
    {
        if (_scriptFilePath is null)
        {
            return;
        }

        ScriptFileIsConverting = true;
        ScriptFileStatus = string.Empty;
        ScriptFileHasError = false;
        try
        {
            var inputPath = _scriptFilePath;
            bool r2d = _scriptFileRomanToDeva;

            Func<string, string?, string> fn = r2d
                ? (text, _) => NepaliScriptConverter.RomanToDevanagari(text)
                : (text, _) => NepaliScriptConverter.DevanagariToRoman(text);

            // R2D: remove the explicit run font so Word applies Devanagari-capable font fallback.
            // D2R: leave fonts unchanged - Latin output is renderable by any standard font.
            Func<string?, string?>? fontMapper = r2d ? (_ => string.Empty) : null;

            var defaultPath = DocumentConverterService.BuildOutputPath(inputPath);
            var outputPath = RequestSaveFilePath?.Invoke(defaultPath);
            if (outputPath is null)
            {
                ScriptFileIsConverting = false;
                return;
            }
            await Task.Run(() => DocumentConverterService.Convert(inputPath, outputPath, fn, fontMapper));

            ScriptFileStatus = _loc.Get("texttools.file_saved") + " " + Path.GetFileName(outputPath);
            ScriptFileHasError = false;
        }
        catch (Exception ex)
        {
            Log.Error($"script file convert: {ex.Message}");
            ScriptFileStatus = ex.Message;
            ScriptFileHasError = true;
        }
        finally
        {
            ScriptFileIsConverting = false;
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // HELPERS
    // ─────────────────────────────────────────────────────────────────────────

    private static void SafeClipboardSet(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return;
        }

        try { System.Windows.Clipboard.SetText(text); }
        catch (Exception ex) { Log.Error($"clipboard set failed: {ex.Message}"); }
    }
}
