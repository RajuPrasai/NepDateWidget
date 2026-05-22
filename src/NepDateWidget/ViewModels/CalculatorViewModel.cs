using NepDateWidget.Helpers;
using NepDateWidget.Services;
using System.Windows.Input;

namespace NepDateWidget.ViewModels;

/// <summary>
/// View model for the Unit tab.
/// Three modes: Land Area (Jagga Napi), Nepali Script, Weight / Volume.
/// All conversion math is pure static; no external dependencies.
/// </summary>
public sealed class UnitViewModel : ViewModelBase
{
    private readonly ILocalizationService _loc;
    private bool _initializing;

    // ═════════════════════════════════════════════════════════════════════════
    // CONVERSION CONSTANTS
    // ═════════════════════════════════════════════════════════════════════════

    // Land area - base unit: square metres.
    // Standard Nepal Survey Department values.
    private static readonly double[] AreaToSqM =
    [
        6772.60,    // 0  Bigha     (20 Kattha = 20 × 338.63)
        338.63,     // 1  Kattha    (20 Dhur)
        16.9315,    // 2  Dhur      (Kattha / 20)
        508.80,     // 3  Ropani    (16 Aana = 16 × 31.80)
        31.8000,    // 4  Aana      (Ropani / 16)
        7.9500,     // 5  Paisa     (Aana / 4)
        1.9875,     // 6  Dam       (Paisa / 4)
        0.09290304, // 7  Sq.Feet
        12718.0,     // 8  Khetmuri  (Terai synonym for Kattha in many districts)
        1.0,        // 9  Sq.Metres
    ];

    private static readonly string[] AreaUnitNames =
    [
        "Bigha", "Kattha", "Dhur",
        "Ropani", "Aana", "Paisa", "Dam",
        "Sq. Feet", "Khetmuri", "Sq. Metres"
    ];

    // Weight / volume - base unit: kilograms.
    private static readonly double[] WeightToKg =
    [
        2.3325,     // 0  Dharni  (= 12 Pawa; UN/Nepal standard ≈ 2.3325 kg)
        0.25,   // 1  Pawa     (Dharni / 12)
        0.568245,   // 2  Mana    (Pathi / 8; volume unit: 1 Mana = 0.5682 L ≈ 0.568 kg)
        4.54596,    // 3  Pathi   (8 Mana; Standard Weights & Measures Act 1968 = 4.54596 L)
        90.9192,    // 4  Muri    (20 Pathi = 20 × 4.54596)
        1.0,        // 5  kg
        0.001,      // 6  g
        1.0,        // 7  litre   (≈ 1 kg for water; grain will differ)
        0.45359237,     // 8  lb      (avoirdupois; International Yard and Pound Agreement 1959, exact)
        0.028349523125, // 9  oz      (avoirdupois; lb / 16, exact)
        1000.0,         // 10 tonne   (metric ton; SI definitional, exact)
        0.0116638038,   // 11 tola    (180 troy grains = 3/8 troy oz; British Indian standard 1833, used in Nepal/India/Pakistan/Bangladesh)
        0.000001,       // 12 mg      (SI definitional, exact)
    ];

    private static readonly string[] WeightUnitNames =
    [
        "Dharni", "Pawa", "Mana", "Pathi", "Muri",
        "kg", "g", "litre",
        "lb", "oz", "tonne", "tola", "mg",
    ];

    // ═════════════════════════════════════════════════════════════════════════
    // MODE SELECTION  (0 = Area, 1 = Script, 2 = Weight)
    // ═════════════════════════════════════════════════════════════════════════

    private int _activeMode;
    public int ActiveMode
    {
        get => _activeMode;
        set
        {
            if (SetProperty(ref _activeMode, value))
            {
                var name = value switch { 0 => "Area", 1 => "Script", 2 => "Weight", _ => value.ToString() };
                Log.Action($"unit mode → {name}");
                OnPropertyChanged(nameof(IsModeArea));
                OnPropertyChanged(nameof(IsModeScript));
                OnPropertyChanged(nameof(IsModeWeight));
            }
        }
    }

    public bool IsModeArea { get => _activeMode == 0; set { if (value) { ActiveMode = 0; } } }
    public bool IsModeScript { get => _activeMode == 1; set { if (value) { ActiveMode = 1; } } }
    public bool IsModeWeight { get => _activeMode == 2; set { if (value) { ActiveMode = 2; } } }

    // ═════════════════════════════════════════════════════════════════════════
    // MODE: LAND AREA
    // ═════════════════════════════════════════════════════════════════════════

    public IReadOnlyList<string> AreaUnits { get; } = AreaUnitNames;

    private string _areaFromValue = "1";
    public string AreaFromValue
    {
        get => _areaFromValue;
        set
        {
            if (SetProperty(ref _areaFromValue, value))
            {
                RecomputeArea();
            }
        }
    }

    private int _areaFromUnit;
    public int AreaFromUnit
    {
        get => _areaFromUnit;
        set
        {
            if (SetProperty(ref _areaFromUnit, value))
            {
                RecomputeArea();
            }
        }
    }

    private int _areaToUnit = 9; // Sq. Metres
    public int AreaToUnit
    {
        get => _areaToUnit;
        set
        {
            if (SetProperty(ref _areaToUnit, value))
            {
                RecomputeArea();
            }
        }
    }

    private string _areaResult = string.Empty;
    public string AreaResult { get => _areaResult; private set => SetProperty(ref _areaResult, value); }

    private bool _areaHasError;
    public bool AreaHasError { get => _areaHasError; private set => SetProperty(ref _areaHasError, value); }

    private string _areaError = string.Empty;
    public string AreaError { get => _areaError; private set => SetProperty(ref _areaError, value); }

    public ICommand SetModeAreaCommand { get; }
    public ICommand AreaCopyCommand { get; }

    // ═════════════════════════════════════════════════════════════════════════
    // MODE: NEPALI SCRIPT CONVERTER
    // ═════════════════════════════════════════════════════════════════════════

    private string _scriptRomanInput = string.Empty;
    public string ScriptRomanInput
    {
        get => _scriptRomanInput;
        set
        {
            if (SetProperty(ref _scriptRomanInput, value))
            {
                ScriptDevaOutput = NepaliScriptConverter.RomanToDevanagari(value);
            }
        }
    }

    private string _scriptDevaOutput = string.Empty;
    public string ScriptDevaOutput
    {
        get => _scriptDevaOutput;
        private set => SetProperty(ref _scriptDevaOutput, value);
    }

    private string _scriptDevaInput = string.Empty;
    public string ScriptDevaInput
    {
        get => _scriptDevaInput;
        set
        {
            if (SetProperty(ref _scriptDevaInput, value))
            {
                ScriptRomanOutput = NepaliScriptConverter.DevanagariToRoman(value);
            }
        }
    }

    private string _scriptRomanOutput = string.Empty;
    public string ScriptRomanOutput
    {
        get => _scriptRomanOutput;
        private set => SetProperty(ref _scriptRomanOutput, value);
    }

    public ICommand SetModeScriptCommand { get; }
    public ICommand CopyDevaCommand { get; }
    public ICommand CopyRomanCommand { get; }

    // ═════════════════════════════════════════════════════════════════════════
    // MODE: WEIGHT / VOLUME
    // ═════════════════════════════════════════════════════════════════════════

    public IReadOnlyList<string> WeightUnits { get; } = WeightUnitNames;

    private string _weightFromValue = "1";
    public string WeightFromValue
    {
        get => _weightFromValue;
        set
        {
            if (SetProperty(ref _weightFromValue, value))
            {
                RecomputeWeight();
            }
        }
    }

    private int _weightFromUnit; // Dharni
    public int WeightFromUnit
    {
        get => _weightFromUnit;
        set
        {
            if (SetProperty(ref _weightFromUnit, value))
            {
                RecomputeWeight();
            }
        }
    }

    private int _weightToUnit = 5; // kg
    public int WeightToUnit
    {
        get => _weightToUnit;
        set
        {
            if (SetProperty(ref _weightToUnit, value))
            {
                RecomputeWeight();
            }
        }
    }

    private string _weightResult = string.Empty;
    public string WeightResult { get => _weightResult; private set => SetProperty(ref _weightResult, value); }

    private bool _weightHasError;
    public bool WeightHasError { get => _weightHasError; private set => SetProperty(ref _weightHasError, value); }

    private string _weightError = string.Empty;
    public string WeightError { get => _weightError; private set => SetProperty(ref _weightError, value); }

    public ICommand SetModeWeightCommand { get; }
    public ICommand WeightCopyCommand { get; }
    public ICommand OpenHelpCommand { get; }

    // ═════════════════════════════════════════════════════════════════════════
    // LABELS
    // ═════════════════════════════════════════════════════════════════════════

    public string ModeLabelArea { get; private set; } = string.Empty;
    public string ModeLabelScript { get; private set; } = string.Empty;
    public string ModeLabelWeight { get; private set; } = string.Empty;
    public string AreaFromLabel { get; private set; } = string.Empty;
    public string AreaToLabel { get; private set; } = string.Empty;
    public string AreaCopyLabel { get; private set; } = string.Empty;
    public string ScriptRomanInLabel { get; private set; } = string.Empty;
    public string ScriptDevaOutLabel { get; private set; } = string.Empty;
    public string ScriptDevaInLabel { get; private set; } = string.Empty;
    public string ScriptRomanOutLabel { get; private set; } = string.Empty;
    public string ScriptCopyLabel { get; private set; } = string.Empty;
    public string ScriptHintLabel { get; private set; } = string.Empty;
    public string WeightFromLabel { get; private set; } = string.Empty;
    public string WeightToLabel { get; private set; } = string.Empty;
    public string WeightCopyLabel { get; private set; } = string.Empty;
    public string HintAreaValue { get; private set; } = string.Empty;
    public string HintWeightValue { get; private set; } = string.Empty;

    // ═════════════════════════════════════════════════════════════════════════
    // CONSTRUCTION
    // ═════════════════════════════════════════════════════════════════════════

    public UnitViewModel(ILocalizationService localizationService)
    {
        _loc = localizationService ?? throw new ArgumentNullException(nameof(localizationService));

        SetModeAreaCommand = new RelayCommand(() => ActiveMode = 0);
        SetModeScriptCommand = new RelayCommand(() => ActiveMode = 1);
        SetModeWeightCommand = new RelayCommand(() => ActiveMode = 2);

        AreaCopyCommand = new RelayCommand(() =>
        {
            if (!string.IsNullOrEmpty(_areaResult))
            {
                TryCopyToClipboard(_areaResult);
                Log.Action("calc area: result copied");
            }
        });

        CopyDevaCommand = new RelayCommand(() =>
        {
            if (!string.IsNullOrEmpty(_scriptDevaOutput))
            {
                TryCopyToClipboard(_scriptDevaOutput);
                Log.Action("calc script: deva output copied");
            }
        });

        CopyRomanCommand = new RelayCommand(() =>
        {
            if (!string.IsNullOrEmpty(_scriptRomanOutput))
            {
                TryCopyToClipboard(_scriptRomanOutput);
                Log.Action("calc script: roman output copied");
            }
        });

        WeightCopyCommand = new RelayCommand(() =>
        {
            if (!string.IsNullOrEmpty(_weightResult))
            {
                TryCopyToClipboard(_weightResult);
                Log.Action("calc weight: result copied");
            }
        });

        OpenHelpCommand = new RelayCommand<string>(key =>
        {
            var shell = System.Windows.Application.Current.Windows
                .OfType<NepDateWidget.Views.ExpandedShellWindow>()
                .FirstOrDefault(w => w.IsVisible)
                ?? (System.Windows.Window)System.Windows.Application.Current.MainWindow!;
            NepDateWidget.Views.HelpPopup.ShowFor(key!, _loc, shell);
        });
        RefreshLabels();
        _initializing = true;
        RecomputeArea();
        RecomputeWeight();
        _initializing = false;
    }

    public void OnLanguageChanged() => RefreshLabels();

    // ═════════════════════════════════════════════════════════════════════════
    // PRIVATE: AREA
    // ═════════════════════════════════════════════════════════════════════════

    private void RecomputeArea()
    {
        AreaHasError = false;
        AreaError = string.Empty;
        AreaResult = string.Empty;

        if (string.IsNullOrWhiteSpace(_areaFromValue))
        {
            return;
        }

        if (!double.TryParse(
                _areaFromValue.Trim(),
                System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture,
                out double input))
        {
            AreaHasError = true;
            AreaError = _loc.Get("unit.error_invalid_number");
            return;
        }

        if (input < 0)
        {
            AreaHasError = true;
            AreaError = _loc.Get("unit.error_negative");
            return;
        }

        int from = Math.Clamp(_areaFromUnit, 0, AreaToSqM.Length - 1);
        int to = Math.Clamp(_areaToUnit, 0, AreaToSqM.Length - 1);

        double sqMetres = input * AreaToSqM[from];
        double result = sqMetres / AreaToSqM[to];

        AreaResult = FormatResult(result);
        if (!_initializing)
        {
            Log.Action($"calc area | {input} {AreaUnitNames[from]} → {AreaResult} {AreaUnitNames[to]}");
        }
    }

    // ═════════════════════════════════════════════════════════════════════════
    // PRIVATE: WEIGHT
    // ═════════════════════════════════════════════════════════════════════════

    private void RecomputeWeight()
    {
        WeightHasError = false;
        WeightError = string.Empty;
        WeightResult = string.Empty;

        if (string.IsNullOrWhiteSpace(_weightFromValue))
        {
            return;
        }

        if (!double.TryParse(
                _weightFromValue.Trim(),
                System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture,
                out double input))
        {
            WeightHasError = true;
            WeightError = _loc.Get("unit.error_invalid_number");
            return;
        }

        if (input < 0)
        {
            WeightHasError = true;
            WeightError = _loc.Get("unit.error_negative");
            return;
        }

        int from = Math.Clamp(_weightFromUnit, 0, WeightToKg.Length - 1);
        int to = Math.Clamp(_weightToUnit, 0, WeightToKg.Length - 1);

        double kg = input * WeightToKg[from];
        double result = kg / WeightToKg[to];

        WeightResult = FormatResult(result);
        if (!_initializing)
        {
            Log.Action($"calc weight | {input} {WeightUnitNames[from]} → {WeightResult} {WeightUnitNames[to]}");
        }
    }

    // ═════════════════════════════════════════════════════════════════════════
    // PRIVATE: LABELS
    // ═════════════════════════════════════════════════════════════════════════

    private void RefreshLabels()
    {
        ModeLabelArea = _loc.Get("unit.mode_area");
        ModeLabelScript = _loc.Get("unit.mode_script");
        ModeLabelWeight = _loc.Get("unit.mode_weight");
        AreaFromLabel = _loc.Get("unit.area.from");
        AreaToLabel = _loc.Get("unit.area.to");
        AreaCopyLabel = _loc.Get("unit.copy");
        ScriptRomanInLabel = _loc.Get("unit.script.roman_in");
        ScriptDevaOutLabel = _loc.Get("unit.script.deva_out");
        ScriptDevaInLabel = _loc.Get("unit.script.deva_in");
        ScriptRomanOutLabel = _loc.Get("unit.script.roman_out");
        ScriptCopyLabel = _loc.Get("unit.copy");
        ScriptHintLabel = _loc.Get("unit.script.hint");
        WeightFromLabel = _loc.Get("unit.weight.from");
        WeightToLabel = _loc.Get("unit.weight.to");
        WeightCopyLabel = _loc.Get("unit.copy");
        HintAreaValue = _loc.Get("hint.area_value");
        HintWeightValue = _loc.Get("hint.weight_value");

        OnPropertyChanged(nameof(ModeLabelArea));
        OnPropertyChanged(nameof(ModeLabelScript));
        OnPropertyChanged(nameof(ModeLabelWeight));
        OnPropertyChanged(nameof(AreaFromLabel));
        OnPropertyChanged(nameof(AreaToLabel));
        OnPropertyChanged(nameof(AreaCopyLabel));
        OnPropertyChanged(nameof(ScriptRomanInLabel));
        OnPropertyChanged(nameof(ScriptDevaOutLabel));
        OnPropertyChanged(nameof(ScriptDevaInLabel));
        OnPropertyChanged(nameof(ScriptRomanOutLabel));
        OnPropertyChanged(nameof(ScriptCopyLabel));
        OnPropertyChanged(nameof(ScriptHintLabel));
        OnPropertyChanged(nameof(WeightFromLabel));
        OnPropertyChanged(nameof(WeightToLabel));
        OnPropertyChanged(nameof(WeightCopyLabel));
        OnPropertyChanged(nameof(HintAreaValue));
        OnPropertyChanged(nameof(HintWeightValue));
    }

    // ═════════════════════════════════════════════════════════════════════════
    // PRIVATE: HELPERS
    // ═════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Formats a result to 4 decimal places, trimming trailing zeros and any trailing dot.
    /// </summary>
    private static string FormatResult(double value)
    {
        string s = value.ToString("F4", System.Globalization.CultureInfo.InvariantCulture);
        if (s.Contains('.'))
        {
            s = s.TrimEnd('0').TrimEnd('.');
        }

        return s;
    }

    private static void TryCopyToClipboard(string text)
    {
        try { System.Windows.Clipboard.SetText(text); }
        catch (Exception ex) { Log.Error($"clipboard set failed: {ex.Message}"); }
    }
}
