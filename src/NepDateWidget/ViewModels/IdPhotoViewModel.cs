using ImageMagick;
using ImageMagick.Drawing;
using NepDateWidget.Helpers;
using NepDateWidget.Services;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;

namespace NepDateWidget.ViewModels;

/// <summary>
/// Manages the ID / Passport Photo sub-view state.
/// Singleton - survives shell expand/collapse cycles.
/// All image processing is done inline via Magick.NET.
/// </summary>
public sealed class IdPhotoViewModel : ViewModelBase
{
    private readonly ILocalizationService _loc;

    // ── Presets ──────────────────────────────────────────────────────────────

    private static readonly IdPhotoPreset[] Presets =
    [
        new(35.0, 45.0),   // 0 Passport / MRP
        new(25.0, 30.0),   // 1 Auto Size
        new(20.0, 25.0),   // 2 Stamp Size
        new(51.0, 51.0),   // 3 US / DV
    ];

    private int _presetIndex;
    public int PresetIndex
    {
        get => _presetIndex;
        set
        {
            int clamped = Math.Clamp(value, 0, 3);
            if (SetProperty(ref _presetIndex, clamped))
            {
                // Populate the editable inputs so the user can see and adjust the values.
                _customWidthText  = FromMm(Presets[clamped].WidthMm,  _unitIndex);
                _customHeightText = FromMm(Presets[clamped].HeightMm, _unitIndex);
                OnPropertyChanged(nameof(CustomWidthText));
                OnPropertyChanged(nameof(CustomHeightText));
                OnPropertyChanged(nameof(TargetWidthMm));
                OnPropertyChanged(nameof(TargetHeightMm));
                OnPropertyChanged(nameof(IsPhotoWidthValid));
                OnPropertyChanged(nameof(IsPhotoHeightValid));
                OnPropertyChanged(nameof(CanExport));
                OnPropertyChanged(nameof(CanExportSheet));
                RaiseCropResetRequested();
            }
        }
    }

    private string _customWidthText = "35.0";
    public string CustomWidthText
    {
        get => _customWidthText;
        set
        {
            if (SetProperty(ref _customWidthText, value))
            {
                OnPropertyChanged(nameof(TargetWidthMm));
                OnPropertyChanged(nameof(IsPhotoWidthValid));
                OnPropertyChanged(nameof(CanExport));
                OnPropertyChanged(nameof(CanExportSheet));
                RaiseCropResetRequested();
            }
        }
    }

    private string _customHeightText = "45.0";
    public string CustomHeightText
    {
        get => _customHeightText;
        set
        {
            if (SetProperty(ref _customHeightText, value))
            {
                OnPropertyChanged(nameof(TargetHeightMm));
                OnPropertyChanged(nameof(IsPhotoHeightValid));
                OnPropertyChanged(nameof(CanExport));
                OnPropertyChanged(nameof(CanExportSheet));
                RaiseCropResetRequested();
            }
        }
    }

    /// <summary>Target width in mm - always derived from the editable text input.</summary>
    public double TargetWidthMm
    {
        get
        {
            if (!double.TryParse(_customWidthText, NumberStyles.Any, CultureInfo.InvariantCulture, out double w) || w <= 0)
                return 0;
            return ToMm(w, _unitIndex);
        }
    }

    /// <summary>Target height in mm - always derived from the editable text input.</summary>
    public double TargetHeightMm
    {
        get
        {
            if (!double.TryParse(_customHeightText, NumberStyles.Any, CultureInfo.InvariantCulture, out double h) || h <= 0)
                return 0;
            return ToMm(h, _unitIndex);
        }
    }

    // ── Photo input ──────────────────────────────────────────────────────────

    private string _photoPath = string.Empty;
    public string PhotoPath
    {
        get => _photoPath;
        private set
        {
            if (SetProperty(ref _photoPath, value))
            {
                OnPropertyChanged(nameof(HasPhoto));
                OnPropertyChanged(nameof(CanExport));
                OnPropertyChanged(nameof(CanExportSheet));
                RaiseCropResetRequested();
            }
        }
    }

    public bool HasPhoto => !string.IsNullOrEmpty(_photoPath);

    // ── Crop shape (0=Square, 1=Circle) ──────────────────────────────────────

    private int _cropShape;
    public int CropShape
    {
        get => _cropShape;
        set
        {
            if (SetProperty(ref _cropShape, Math.Clamp(value, 0, 1)))
            {
                RaiseCropResetRequested();
            }
        }
    }

    // ── Crop (normalized 0.0–1.0 relative to source image pixel dimensions) ─

    private double _cropNormX;
    private double _cropNormY;
    private double _cropNormW;
    private double _cropNormH;

    /// <summary>
    /// Called by the view code-behind whenever the user repositions the crop frame.
    /// Values are normalized fractions of the source image pixel dimensions.
    /// </summary>
    public void SetCropNorm(double normX, double normY, double normW, double normH)
    {
        _cropNormX = normX;
        _cropNormY = normY;
        _cropNormW = normW;
        _cropNormH = normH;
    }

    // ── Unit system ───────────────────────────────────────────────────────────
    // 0=mm  1=cm  2=in  3=px (at 300 DPI)

    private int _unitIndex;
    public int UnitIndex
    {
        get => _unitIndex;
        set
        {
            int clamped = Math.Clamp(value, 0, 3);
            if (_unitIndex == clamped) return;

            int oldUnit = _unitIndex;
            _unitIndex = clamped;

            // Convert custom dimension text to preserve physical size across unit changes.
            // Bypasses property setters to avoid cascading notifications before we fire them below.
            ConvertCustomTextsToUnit(oldUnit, clamped);

            OnPropertyChanged(nameof(UnitIndex));
            OnPropertyChanged(nameof(UnitSuffix));
            OnPropertyChanged(nameof(TargetWidthMm));
            OnPropertyChanged(nameof(TargetHeightMm));
            OnPropertyChanged(nameof(IsPhotoWidthValid));
            OnPropertyChanged(nameof(IsPhotoHeightValid));
            OnPropertyChanged(nameof(CanExport));
            OnPropertyChanged(nameof(CanExportSheet));
            RaiseCropResetRequested();
        }
    }

    private void ConvertCustomTextsToUnit(int oldUnit, int newUnit)
    {
        if (oldUnit == newUnit) return;

        if (double.TryParse(_customWidthText, NumberStyles.Any, CultureInfo.InvariantCulture, out double w) && w > 0)
        {
            _customWidthText = FromMm(ToMm(w, oldUnit), newUnit);
            OnPropertyChanged(nameof(CustomWidthText));
        }

        if (double.TryParse(_customHeightText, NumberStyles.Any, CultureInfo.InvariantCulture, out double h) && h > 0)
        {
            _customHeightText = FromMm(ToMm(h, oldUnit), newUnit);
            OnPropertyChanged(nameof(CustomHeightText));
        }
    }

    private static double ToMm(double value, int unit) => unit switch
    {
        1 => value * 10.0,
        2 => value * 25.4,
        3 => value / 300.0 * 25.4,
        _ => value,
    };

    private static string FromMm(double mm, int unit) => unit switch
    {
        1 => (mm / 10.0).ToString("F2", CultureInfo.InvariantCulture),
        2 => (mm / 25.4).ToString("F3", CultureInfo.InvariantCulture),
        3 => Math.Round(mm / 25.4 * 300.0).ToString(CultureInfo.InvariantCulture),
        _ => mm.ToString("F1", CultureInfo.InvariantCulture),
    };

    /// <summary>Short suffix for the selected unit (mm, cm, in, px).</summary>
    public string UnitSuffix => _unitIndex switch
    {
        1 => "cm",
        2 => "in",
        3 => "px",
        _ => "mm",
    };

    // ── Sheet size ────────────────────────────────────────────────────────────
    // SheetSizes are lookup-only: selecting a preset populates the editable mm inputs.
    // 0=A4  1=4R  2=3R  3=5R  - mm values at 300 DPI

    private static readonly (double WMm, double HMm)[] SheetPresets =
    [
        (210.0, 297.0),   // A4
        (102.0, 152.0),   // 4R = 4×6"
        ( 89.0, 127.0),   // 3R = 3.5×5"
        (127.0, 178.0),   // 5R = 5×7"
    ];

    private int _sheetSizeIndex;
    public int SheetSizeIndex
    {
        get => _sheetSizeIndex;
        set
        {
            int clamped = Math.Clamp(value, 0, 3);
            if (SetProperty(ref _sheetSizeIndex, clamped))
            {
                _sheetWidthMmText  = FromSheetMm(SheetPresets[clamped].WMm, _sheetUnitIndex);
                _sheetHeightMmText = FromSheetMm(SheetPresets[clamped].HMm, _sheetUnitIndex);
                OnPropertyChanged(nameof(SheetWidthMmText));
                OnPropertyChanged(nameof(SheetHeightMmText));
                OnPropertyChanged(nameof(IsSheetWidthValid));
                OnPropertyChanged(nameof(IsSheetHeightValid));
                OnPropertyChanged(nameof(CanExportSheet));
            }
        }
    }

    private string _sheetWidthMmText = "210";
    public string SheetWidthMmText
    {
        get => _sheetWidthMmText;
        set
        {
            if (SetProperty(ref _sheetWidthMmText, value ?? ""))
            {
                OnPropertyChanged(nameof(IsSheetWidthValid));
                OnPropertyChanged(nameof(CanExportSheet));
            }
        }
    }

    private string _sheetHeightMmText = "297";
    public string SheetHeightMmText
    {
        get => _sheetHeightMmText;
        set
        {
            if (SetProperty(ref _sheetHeightMmText, value ?? ""))
            {
                OnPropertyChanged(nameof(IsSheetHeightValid));
                OnPropertyChanged(nameof(CanExportSheet));
            }
        }
    }

    // 0=mm  1=cm  2=in
    private int _sheetUnitIndex;
    public int SheetUnitIndex
    {
        get => _sheetUnitIndex;
        set
        {
            int clamped = Math.Clamp(value, 0, 2);
            if (_sheetUnitIndex == clamped) return;
            int oldUnit = _sheetUnitIndex;
            _sheetUnitIndex = clamped;
            ConvertSheetTextsToUnit(oldUnit, clamped);
            OnPropertyChanged(nameof(SheetUnitIndex));
            OnPropertyChanged(nameof(IsSheetWidthValid));
            OnPropertyChanged(nameof(IsSheetHeightValid));
            OnPropertyChanged(nameof(CanExportSheet));
        }
    }

    private void ConvertSheetTextsToUnit(int oldUnit, int newUnit)
    {
        if (oldUnit == newUnit) return;

        if (double.TryParse(_sheetWidthMmText, NumberStyles.Any, CultureInfo.InvariantCulture, out double w) && w > 0)
        {
            _sheetWidthMmText = FromSheetMm(ToSheetMm(w, oldUnit), newUnit);
            OnPropertyChanged(nameof(SheetWidthMmText));
        }

        if (double.TryParse(_sheetHeightMmText, NumberStyles.Any, CultureInfo.InvariantCulture, out double h) && h > 0)
        {
            _sheetHeightMmText = FromSheetMm(ToSheetMm(h, oldUnit), newUnit);
            OnPropertyChanged(nameof(SheetHeightMmText));
        }
    }

    private static double ToSheetMm(double value, int unit) => unit switch
    {
        1 => value * 10.0,
        2 => value * 25.4,
        _ => value,
    };

    private static string FromSheetMm(double mm, int unit) => unit switch
    {
        1 => (mm / 10.0).ToString("F1", CultureInfo.InvariantCulture),
        2 => (mm / 25.4).ToString("F2", CultureInfo.InvariantCulture),
        _ => mm.ToString("F0", CultureInfo.InvariantCulture),
    };

    // 0 = Auto (best-fit rotation), 1 = Portrait, 2 = Landscape
    private int _sheetOrientation;
    public int SheetOrientation
    {
        get => _sheetOrientation;
        set => SetProperty(ref _sheetOrientation, Math.Clamp(value, 0, 2));
    }

    // Empty string means auto-calculate for cols or rows.
    private string _sheetColsText = "";
    public string SheetColsText
    {
        get => _sheetColsText;
        set
        {
            if (SetProperty(ref _sheetColsText, value ?? ""))
            {
                OnPropertyChanged(nameof(IsSheetColsValid));
                OnPropertyChanged(nameof(CanExportSheet));
            }
        }
    }

    private string _sheetRowsText = "";
    public string SheetRowsText
    {
        get => _sheetRowsText;
        set
        {
            if (SetProperty(ref _sheetRowsText, value ?? ""))
            {
                OnPropertyChanged(nameof(IsSheetRowsValid));
                OnPropertyChanged(nameof(CanExportSheet));
            }
        }
    }

    // ── Job state ─────────────────────────────────────────────────────────────

    private bool _isJobRunning;
    public bool IsJobRunning
    {
        get => _isJobRunning;
        private set
        {
            if (SetProperty(ref _isJobRunning, value))
            {
                OnPropertyChanged(nameof(CanExport));
                OnPropertyChanged(nameof(CanExportSheet));
            }
        }
    }

    private string _statusText = string.Empty;
    public string StatusText
    {
        get => _statusText;
        private set => SetProperty(ref _statusText, value);
    }

    private bool _hasStatus;
    public bool HasStatus
    {
        get => _hasStatus;
        private set => SetProperty(ref _hasStatus, value);
    }

    private bool _statusIsError;
    public bool StatusIsError
    {
        get => _statusIsError;
        private set => SetProperty(ref _statusIsError, value);
    }

    private DispatcherTimer? _resetTimer;

    // ── Validation limits and properties ─────────────────────────────────────

    private const double PhotoDimMinMm = 10.0;   // 10 mm - smallest practical ID photo dimension
    private const double PhotoDimMaxMm = 250.0;  // 250 mm - larger than A4 short edge
    private const double SheetDimMinMm = 50.0;   // 50 mm - smallest practical paper dimension
    private const double SheetDimMaxMm = 1200.0; // 1200 mm - covers A0 (841×1189 mm) with margin
    private const int    ColsRowsMax   = 50;     // max columns or rows on a sheet

    public bool IsPhotoWidthValid  => TargetWidthMm  >= PhotoDimMinMm && TargetWidthMm  <= PhotoDimMaxMm;
    public bool IsPhotoHeightValid => TargetHeightMm >= PhotoDimMinMm && TargetHeightMm <= PhotoDimMaxMm;
    public bool IsSheetWidthValid
    {
        get
        {
            if (!double.TryParse(_sheetWidthMmText, NumberStyles.Any, CultureInfo.InvariantCulture, out double v)) return false;
            double mm = ToSheetMm(v, _sheetUnitIndex);
            return mm >= SheetDimMinMm && mm <= SheetDimMaxMm;
        }
    }
    public bool IsSheetHeightValid
    {
        get
        {
            if (!double.TryParse(_sheetHeightMmText, NumberStyles.Any, CultureInfo.InvariantCulture, out double v)) return false;
            double mm = ToSheetMm(v, _sheetUnitIndex);
            return mm >= SheetDimMinMm && mm <= SheetDimMaxMm;
        }
    }
    public bool IsSheetColsValid   => string.IsNullOrWhiteSpace(_sheetColsText) || (int.TryParse(_sheetColsText, out int sc) && sc >= 1 && sc <= ColsRowsMax);
    public bool IsSheetRowsValid   => string.IsNullOrWhiteSpace(_sheetRowsText) || (int.TryParse(_sheetRowsText, out int sr) && sr >= 1 && sr <= ColsRowsMax);

    public bool CanExport     => HasPhoto && !_isJobRunning && IsPhotoWidthValid && IsPhotoHeightValid;
    public bool CanExportSheet => CanExport && IsSheetWidthValid && IsSheetHeightValid && IsSheetColsValid && IsSheetRowsValid;

    // ── Commands ──────────────────────────────────────────────────────────────

    public ICommand BrowseCommand { get; }
    public ICommand ExportPhotoCommand { get; }
    public ICommand ExportSheetCommand { get; }
    public ICommand ClearCommand { get; }

    // ── Crop reset event ──────────────────────────────────────────────────────

    /// <summary>
    /// Raised whenever the photo or format changes so the view can reset the crop overlay.
    /// </summary>
    public event Action? CropResetRequested;

    private void RaiseCropResetRequested()
    {
        if (HasPhoto)
        {
            CropResetRequested?.Invoke();
        }
    }

    // ── Localized labels ──────────────────────────────────────────────────────

    public string DropLabel          => _loc.Get("idphoto.drop_label");
    public string BrowseLabel         => _loc.Get("idphoto.browse");
    public string FormatSectionLabel  => _loc.Get("idphoto.format_section");
    public string Preset0Label        => _loc.Get("idphoto.preset_passport");
    public string Preset1Label        => _loc.Get("idphoto.preset_auto");
    public string Preset2Label        => _loc.Get("idphoto.preset_stamp");
    public string Preset3Label        => _loc.Get("idphoto.preset_usdv");
    public string WidthMmLabel        => _loc.Get("idphoto.width_label");
    public string HeightMmLabel       => _loc.Get("idphoto.height_label");
    public string SheetSectionLabel   => _loc.Get("idphoto.sheet_section");
    public string Sheet0Label         => _loc.Get("idphoto.sheet_a4");
    public string Sheet1Label         => _loc.Get("idphoto.sheet_4r");
    public string Sheet2Label         => _loc.Get("idphoto.sheet_3r");
    public string Sheet3Label         => _loc.Get("idphoto.sheet_5r");
    public string SheetDimsLabel      => _loc.Get("idphoto.sheet_dims_label");
    public string SheetWidthLabel     => _loc.Get("idphoto.sheet_width_label");
    public string SheetHeightLabel    => _loc.Get("idphoto.sheet_height_label");
    public string LayoutLabel         => _loc.Get("idphoto.layout_label");
    public string OrientationLabel    => _loc.Get("idphoto.orientation_label");
    public string AutoLabel           => _loc.Get("idphoto.auto_label");
    public string PortraitLabel       => _loc.Get("idphoto.portrait");
    public string LandscapeLabel      => _loc.Get("idphoto.landscape");
    public string ColsLabel           => _loc.Get("idphoto.cols_label");
    public string RowsLabel           => _loc.Get("idphoto.rows_label");
    public string CropHintLabel       => _loc.Get("idphoto.crop_hint");
    public string UnitLabel           => _loc.Get("idphoto.unit_label");
    public string SheetUnitLabel      => _loc.Get("idphoto.unit_label");
    public string SavePhotoLabel      => _loc.Get("idphoto.save_photo");
    public string SaveSheetLabel      => _loc.Get("idphoto.save_sheet");
    public string ClearLabel          => _loc.Get("idphoto.reset");
    public string CropSquareLabel     => _loc.Get("idphoto.crop_square");
    public string CropCircleLabel     => _loc.Get("idphoto.crop_circle");

    // ── Construction ──────────────────────────────────────────────────────────

    public IdPhotoViewModel(ILocalizationService localizationService)
    {
        _loc = localizationService ?? throw new ArgumentNullException(nameof(localizationService));

        BrowseCommand      = new RelayCommand(DoBrowse);
        ExportPhotoCommand = new RelayCommand(DoExportPhoto);
        ExportSheetCommand = new RelayCommand(DoExportSheet);
        ClearCommand       = new RelayCommand(DoClear);
    }

    // ── Commands impl ─────────────────────────────────────────────────────────

    private void DoBrowse()
    {
        var dlg = new Microsoft.Win32.OpenFileDialog
        {
            Title  = _loc.Get("idphoto.drop_label"),
            Filter = "Images|*.jpg;*.jpeg;*.png;*.webp;*.bmp;*.tiff;*.tif",
        };

        if (dlg.ShowDialog() != true)
        {
            return;
        }

        CancelPendingReset();
        PhotoPath = dlg.FileName;
    }

    private void DoClear()
    {
        CancelPendingReset();
        PhotoPath = string.Empty;
        StatusText = string.Empty;
        HasStatus = false;
    }

    /// <summary>
    /// Accepts a file path from drag-drop in the view code-behind.
    /// Replaces the current photo (deduplication via path replace).
    /// </summary>
    public void SetPhoto(string path)
    {
        if (!File.Exists(path))
        {
            return;
        }

        CancelPendingReset();
        StatusText = string.Empty;
        HasStatus = false;
        PhotoPath = path;
    }

    private async void DoExportPhoto()
    {
        if (!CanExport)
        {
            return;
        }

        bool isCircle = _cropShape == 1;
        var dlg = new Microsoft.Win32.SaveFileDialog
        {
            Title            = _loc.Get("idphoto.save_photo"),
            Filter           = isCircle ? "PNG Image|*.png" : "JPEG Image|*.jpg",
            DefaultExt       = isCircle ? ".png" : ".jpg",
            FileName         = isCircle ? "id_photo_circle" : "id_photo",
        };

        if (dlg.ShowDialog() != true)
        {
            return;
        }

        await RunExport(async () =>
        {
            await Task.Run(() => ExportSinglePhoto(dlg.FileName));
            return (false, _loc.Get("idphoto.done_photo"));
        });
    }

    private async void DoExportSheet()
    {
        if (!CanExport)
        {
            return;
        }

        var dlg = new Microsoft.Win32.SaveFileDialog
        {
            Title      = _loc.Get("idphoto.save_sheet"),
            Filter     = "JPEG Image|*.jpg",
            DefaultExt = ".jpg",
            FileName   = "id_photo_sheet",
        };

        if (dlg.ShowDialog() != true)
        {
            return;
        }

        await RunExport(async () =>
        {
            string tempExt = _cropShape == 1 ? ".png" : ".jpg";
            string tempPhoto = Path.Combine(Path.GetTempPath(), $"idphoto_{Guid.NewGuid():N}{tempExt}");
            try
            {
                await Task.Run(() =>
                {
                    ExportSinglePhoto(tempPhoto);
                    ExportPrintSheet(tempPhoto, dlg.FileName);
                });
            }
            finally
            {
                try { File.Delete(tempPhoto); } catch { /* best-effort */ }
            }

            return (false, _loc.Get("idphoto.done_sheet"));
        });
    }

    private async Task RunExport(Func<Task<(bool isError, string message)>> work)
    {
        IsJobRunning = true;
        StatusText = _loc.Get("idphoto.processing");
        HasStatus = true;
        StatusIsError = false;

        bool isError = false;
        string message = string.Empty;

        try
        {
            (isError, message) = await work();
        }
        catch (Exception ex)
        {
            isError = true;
            message = ex.Message;
        }
        finally
        {
            IsJobRunning = false;
        }

        StatusText = message;
        StatusIsError = isError;
        HasStatus = true;

        int delaySec = isError ? 8 : 3;
        CancelPendingReset();
        _resetTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(delaySec) };
        _resetTimer.Tick += (_, _) =>
        {
            _resetTimer?.Stop();
            _resetTimer = null;
            StatusText = string.Empty;
            HasStatus = false;
            StatusIsError = false;
        };
        _resetTimer.Start();
    }

    private void CancelPendingReset()
    {
        _resetTimer?.Stop();
        _resetTimer = null;
    }

    // ── Core image processing ─────────────────────────────────────────────────

    private void ExportSinglePhoto(string outputPath)
    {
        double wMm = TargetWidthMm;
        double hMm = TargetHeightMm;
        const double dpi = 300.0;

        int targetW = (int)Math.Round(wMm / 25.4 * dpi);
        int targetH = (int)Math.Round(hMm / 25.4 * dpi);

        using var image = new MagickImage(_photoPath);

        // Bake EXIF rotation into pixels before computing dimensions or cropping.
        // Without this, photos taken with phones (which store orientation as EXIF metadata
        // and leave raw pixels unrotated) produce a crop that does not match the display.
        image.AutoOrient();

        // Crop to the user-specified region (normalized → pixels)
        int imgW = (int)image.Width;
        int imgH = (int)image.Height;

        // Fall back to full image if crop was never initialized (norms are zero).
        bool hasCrop = _cropNormW > 0 && _cropNormH > 0;
        int cropX = hasCrop ? (int)Math.Round(_cropNormX * imgW) : 0;
        int cropY = hasCrop ? (int)Math.Round(_cropNormY * imgH) : 0;
        int cropW = hasCrop ? (int)Math.Round(_cropNormW * imgW) : imgW;
        int cropH = hasCrop ? (int)Math.Round(_cropNormH * imgH) : imgH;

        cropX = Math.Clamp(cropX, 0, imgW - 1);
        cropY = Math.Clamp(cropY, 0, imgH - 1);
        cropW = Math.Clamp(cropW, 1, imgW - cropX);
        cropH = Math.Clamp(cropH, 1, imgH - cropY);

        image.Crop(new MagickGeometry(cropX, cropY, (uint)cropW, (uint)cropH));

        // Resize to exact target dimensions
        image.Resize(new MagickGeometry((uint)targetW, (uint)targetH) { IgnoreAspectRatio = true });

        image.Density = new Density(dpi, dpi);
        image.Quality = 95;

        if (_cropShape == 1)
        {
            // Apply circular mask - pixels outside the inscribed ellipse become transparent.
            using var mask = new MagickImage(MagickColors.Transparent, (uint)targetW, (uint)targetH);
            mask.Alpha(AlphaOption.On);
            new Drawables()
                .StrokeColor(MagickColors.None)
                .FillColor(MagickColors.White)
                .Ellipse(targetW / 2.0, targetH / 2.0, targetW / 2.0, targetH / 2.0, 0, 360)
                .Draw(mask);
            image.Alpha(AlphaOption.On);
            image.Composite(mask, CompositeOperator.DstIn);
            image.Format = MagickFormat.Png;
        }
        else
        {
            // Flatten alpha to white before JPEG save.
            image.BackgroundColor = MagickColors.White;
            image.Alpha(AlphaOption.Remove);
            image.Format = MagickFormat.Jpeg;
        }

        image.Write(outputPath);
    }

    private void ExportPrintSheet(string photoPath, string sheetPath)
    {
        double wMm = TargetWidthMm;
        double hMm = TargetHeightMm;
        const double dpi = 300.0;

        int photoW = (int)Math.Round(wMm / 25.4 * dpi);
        int photoH = (int)Math.Round(hMm / 25.4 * dpi);

        // Resolve paper dimensions from the editable mm inputs (preset selection fills these).
        double sheetWMm = double.TryParse(_sheetWidthMmText,  NumberStyles.Any, CultureInfo.InvariantCulture, out var sw) ? ToSheetMm(sw, _sheetUnitIndex) : 210.0;
        double sheetHMm = double.TryParse(_sheetHeightMmText, NumberStyles.Any, CultureInfo.InvariantCulture, out var sh) ? ToSheetMm(sh, _sheetUnitIndex) : 297.0;
        int sheetW = (int)Math.Round(sheetWMm / 25.4 * dpi);
        int sheetH = (int)Math.Round(sheetHMm / 25.4 * dpi);

        // Resolve cols and rows: explicit value if typed, otherwise auto-calculate.
        bool userCols = int.TryParse(_sheetColsText, out int parsedCols) && parsedCols >= 1;
        bool userRows = int.TryParse(_sheetRowsText, out int parsedRows) && parsedRows >= 1;

        const int minGap = 4;
        bool rotate90;
        int cols, rows;

        if (_sheetOrientation == 0)
        {
            // Auto: pick whichever orientation fits more photos.
            int portCols = Math.Max(1, (sheetW - minGap) / (photoW + minGap));
            int portRows = Math.Max(1, (sheetH - minGap) / (photoH + minGap));
            int landCols = Math.Max(1, (sheetW - minGap) / (photoH + minGap));
            int landRows = Math.Max(1, (sheetH - minGap) / (photoW + minGap));
            rotate90 = (landCols * landRows) > (portCols * portRows);
            cols = userCols ? parsedCols : (rotate90 ? landCols : portCols);
            rows = userRows ? parsedRows : (rotate90 ? landRows : portRows);
        }
        else
        {
            rotate90 = _sheetOrientation == 2; // 1=Portrait, 2=Landscape
            int autoW = rotate90 ? photoH : photoW;
            int autoH = rotate90 ? photoW : photoH;
            cols = userCols ? parsedCols : Math.Max(1, (sheetW - minGap) / (autoW + minGap));
            rows = userRows ? parsedRows : Math.Max(1, (sheetH - minGap) / (autoH + minGap));
        }

        int placedW = rotate90 ? photoH : photoW;
        int placedH = rotate90 ? photoW : photoH;

        // Derive the exact gap that makes every horizontal space identical (left margin =
        // inter-photo gap = right margin) and every vertical space identical, using integer
        // division. The rounding remainder is at most (cols) or (rows) extra pixels on the
        // far edge - imperceptible at 300 DPI.
        int gx = (sheetW - cols * placedW) / (cols + 1);
        int gy = (sheetH - rows * placedH) / (rows + 1);

        using var photo = new MagickImage(photoPath);
        if (rotate90) photo.Rotate(90);

        using var sheet = new MagickImage(MagickColors.White, (uint)sheetW, (uint)sheetH);
        sheet.Density = new Density(dpi, dpi);

        for (int row = 0; row < rows; row++)
        {
            for (int col = 0; col < cols; col++)
            {
                int x = gx + col * (placedW + gx);
                int y = gy + row * (placedH + gy);
                sheet.Composite(photo, x, y, CompositeOperator.Over);
            }
        }

        sheet.Quality = 95;
        sheet.Format = MagickFormat.Jpeg;
        sheet.Write(sheetPath);
    }

    // ── Public API for OnLanguageChanged ─────────────────────────────────────

    public void OnLanguageChanged()
    {
        OnPropertyChanged(nameof(DropLabel));
        OnPropertyChanged(nameof(BrowseLabel));
        OnPropertyChanged(nameof(FormatSectionLabel));
        OnPropertyChanged(nameof(Preset0Label));
        OnPropertyChanged(nameof(Preset1Label));
        OnPropertyChanged(nameof(Preset2Label));
        OnPropertyChanged(nameof(Preset3Label));
        OnPropertyChanged(nameof(WidthMmLabel));
        OnPropertyChanged(nameof(HeightMmLabel));
        OnPropertyChanged(nameof(SheetSectionLabel));
        OnPropertyChanged(nameof(Sheet0Label));
        OnPropertyChanged(nameof(Sheet1Label));
        OnPropertyChanged(nameof(Sheet2Label));
        OnPropertyChanged(nameof(Sheet3Label));
        OnPropertyChanged(nameof(SheetDimsLabel));
        OnPropertyChanged(nameof(SheetWidthLabel));
        OnPropertyChanged(nameof(SheetHeightLabel));
        OnPropertyChanged(nameof(LayoutLabel));
        OnPropertyChanged(nameof(OrientationLabel));
        OnPropertyChanged(nameof(AutoLabel));
        OnPropertyChanged(nameof(PortraitLabel));
        OnPropertyChanged(nameof(LandscapeLabel));
        OnPropertyChanged(nameof(ColsLabel));
        OnPropertyChanged(nameof(RowsLabel));
        OnPropertyChanged(nameof(CropHintLabel));
        OnPropertyChanged(nameof(UnitLabel));
        OnPropertyChanged(nameof(SheetUnitLabel));
        OnPropertyChanged(nameof(SavePhotoLabel));
        OnPropertyChanged(nameof(SaveSheetLabel));
        OnPropertyChanged(nameof(ClearLabel));
        OnPropertyChanged(nameof(CropSquareLabel));
        OnPropertyChanged(nameof(CropCircleLabel));
    }
}

/// <summary>Preset dimensions for ID photo formats.</summary>
internal sealed record IdPhotoPreset(double WidthMm, double HeightMm)
{
    public double AspectRatio => WidthMm / HeightMm;
}
