using NepDateWidget.Helpers;
using NepDateWidget.Models;
using NepDateWidget.Services;
using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using static NepDateWidget.Helpers.FileFormatHelper;

namespace NepDateWidget.ViewModels;

/// <summary>
/// Unified image tools view — manages Compress, Resize, and Convert operations as
/// independently stackable toggles. Singleton; survives shell expand/collapse cycles.
/// </summary>
public sealed class ImageToolsViewModel : ViewModelBase
{
    private readonly IFileTypeService _fileTypeService;
    private readonly IJobOrchestrationService _orchestrator;
    private readonly IImageConversionService _conversionService;
    private readonly ILocalizationService _loc;

    private DispatcherTimer? _resetTimer;

    // ── Supported extensions for file filter and path gating ─────────────────

    private static readonly HashSet<string> _supportedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".png", ".webp", ".avif", ".gif", ".bmp",
        ".tif", ".tiff", ".heic", ".heif", ".ico", ".tga",
        ".arw", ".cr2", ".cr3", ".dng", ".nef", ".orf", ".raf",
        ".rw2", ".erf", ".pef", ".x3f",
        ".pdf",
    };

    // ── File list ─────────────────────────────────────────────────────────────

    public ObservableCollection<CompressionFileItemViewModel> Files { get; } = new();

    // ── Detected type tracking ────────────────────────────────────────────────

    private string _detectedMimeType = string.Empty;
    private FileCategory _detectedCategory = FileCategory.Unsupported;
    private bool _hasMultipleMimeTypes;
    private bool _hasAnyPdfFile;

    public bool HasFiles => Files.Count > 0;
    public bool ShowFileList => HasFiles && !ShowSummary;
    public bool HasAnyPdfFile => _hasAnyPdfFile;

    // ── Toggle state ──────────────────────────────────────────────────────────

    private bool _isCompressEnabled;
    public bool IsCompressEnabled
    {
        get => _isCompressEnabled;
        set
        {
            if (SetProperty(ref _isCompressEnabled, value))
            {
                _userHasManuallyChangedToggles = true;
                _compressDisabledByFormat = false; // user explicitly overrode the format constraint
                OnPropertyChanged(nameof(ShowCompressSection));
                OnPropertyChanged(nameof(ShowQualitySlider));
                OnPropertyChanged(nameof(ShowGifOptions));
                OnPropertyChanged(nameof(ShowTiffOptions));
                OnPropertyChanged(nameof(ShowPdfOptions));
                OnPropertyChanged(nameof(CanRun));
                OnPropertyChanged(nameof(ActionButtonLabel));
                OnPropertyChanged(nameof(IsRunButtonEnabled));
            }
        }
    }

    private bool _isResizeEnabled;
    public bool IsResizeEnabled
    {
        get => _isResizeEnabled;
        set
        {
            if (SetProperty(ref _isResizeEnabled, value))
            {
                _userHasManuallyChangedToggles = true;
                OnPropertyChanged(nameof(ShowResizeSection));
                OnPropertyChanged(nameof(CanRun));
                OnPropertyChanged(nameof(ActionButtonLabel));
                OnPropertyChanged(nameof(IsRunButtonEnabled));
            }
        }
    }

    private bool _isConvertEnabled;
    public bool IsConvertEnabled
    {
        get => _isConvertEnabled;
        set
        {
            if (SetProperty(ref _isConvertEnabled, value))
            {
                _userHasManuallyChangedToggles = true;
                OnPropertyChanged(nameof(ShowConvertSection));
                OnPropertyChanged(nameof(ShowQualitySlider));
                OnPropertyChanged(nameof(CanRun));
                OnPropertyChanged(nameof(ActionButtonLabel));
                OnPropertyChanged(nameof(IsRunButtonEnabled));
                ReEvaluateMixedTypeWarning();
                EnforceFormatConstraints();
            }
        }
    }

    // ── Toggle enabled (CanToggleXxx) ─────────────────────────────────────────

    public bool CanToggleCompress => !_isJobRunning && _detectedCategory != FileCategory.Raw;
    public bool CanToggleResize   => !_isJobRunning && !_hasAnyPdfFile;
    public bool CanToggleConvert  => !_isJobRunning && !_hasAnyPdfFile;

    public bool IsRawOnlyFiles => _detectedCategory == FileCategory.Raw;

    // ── Section visibility ────────────────────────────────────────────────────

    public bool ShowCompressSection => _isCompressEnabled;
    public bool ShowResizeSection   => _isResizeEnabled && !_hasAnyPdfFile;
    public bool ShowConvertSection  => _isConvertEnabled && !_hasAnyPdfFile;

    // ── Quality slider ────────────────────────────────────────────────────────

    /// <summary>
    /// Hidden when Compress is OFF. Hidden when Convert is ON targeting a lossless format.
    /// "Lossy" formats: jpg, webp, avif — matches existing ImageConverterViewModel.ShowQuality.
    /// </summary>
    public bool ShowQualitySlider =>
        _isCompressEnabled &&
        (!_isConvertEnabled || _selectedFormatExt is "jpg" or "webp" or "avif");

    // ── Quality level ─────────────────────────────────────────────────────────

    private int _qualityLevel = 1;
    public int QualityLevel
    {
        get => _qualityLevel;
        set
        {
            if (SetProperty(ref _qualityLevel, Math.Clamp(value, 0, 4)))
                RecomputeProjectedDimensions();
        }
    }

    // ── Format selection ──────────────────────────────────────────────────────

    private string _selectedFormatExt = "jpg";
    public string SelectedFormatExt => _selectedFormatExt;

    public bool IsFormatJpeg => _selectedFormatExt == "jpg";
    public bool IsFormatPng  => _selectedFormatExt == "png";
    public bool IsFormatWebp => _selectedFormatExt == "webp";
    public bool IsFormatAvif => _selectedFormatExt == "avif";
    public bool IsFormatGif  => _selectedFormatExt == "gif";
    public bool IsFormatBmp  => _selectedFormatExt == "bmp";
    public bool IsFormatTiff => _selectedFormatExt == "tif";
    public bool IsFormatIco  => _selectedFormatExt == "ico";
    public bool IsFormatTga  => _selectedFormatExt == "tga";

    private static readonly HashSet<string> _noCompressionFormats =
        new(StringComparer.OrdinalIgnoreCase) { "gif", "bmp", "ico", "tga" };

    private bool _compressDisabledByFormat;
    private bool _isSummaryVisible;

    private void SetFormat(string ext)
    {
        if (_isJobRunning) return;
        if (SetProperty(ref _selectedFormatExt, ext, nameof(SelectedFormatExt)))
        {
            OnPropertyChanged(nameof(IsFormatJpeg));
            OnPropertyChanged(nameof(IsFormatPng));
            OnPropertyChanged(nameof(IsFormatWebp));
            OnPropertyChanged(nameof(IsFormatAvif));
            OnPropertyChanged(nameof(IsFormatGif));
            OnPropertyChanged(nameof(IsFormatBmp));
            OnPropertyChanged(nameof(IsFormatTiff));
            OnPropertyChanged(nameof(IsFormatIco));
            OnPropertyChanged(nameof(IsFormatTga));
            OnPropertyChanged(nameof(ShowQualitySlider));
            EnforceFormatConstraints();
        }
    }

    /// <summary>
    /// Auto-disables compression when the selected output format has no meaningful
    /// quality-based compression (gif, bmp, ico, tga). Restores compression when
    /// switching to a format that supports it, if it was only disabled by this rule.
    /// </summary>
    private void EnforceFormatConstraints()
    {
        bool formatBlocksCompress = _isConvertEnabled
            && _noCompressionFormats.Contains(_selectedFormatExt);

        if (formatBlocksCompress && _isCompressEnabled)
        {
            _isCompressEnabled = false;
            _compressDisabledByFormat = true;
            OnPropertyChanged(nameof(IsCompressEnabled));
            OnPropertyChanged(nameof(ShowCompressSection));
            OnPropertyChanged(nameof(ShowGifOptions));
            OnPropertyChanged(nameof(ShowTiffOptions));
            OnPropertyChanged(nameof(ShowPdfOptions));
            OnPropertyChanged(nameof(ShowQualitySlider));
            OnPropertyChanged(nameof(CanRun));
            OnPropertyChanged(nameof(IsRunButtonEnabled));
            OnPropertyChanged(nameof(ActionButtonLabel));
        }
        else if (!formatBlocksCompress && !_isCompressEnabled && _compressDisabledByFormat)
        {
            _isCompressEnabled = true;
            _compressDisabledByFormat = false;
            OnPropertyChanged(nameof(IsCompressEnabled));
            OnPropertyChanged(nameof(ShowCompressSection));
            OnPropertyChanged(nameof(ShowGifOptions));
            OnPropertyChanged(nameof(ShowTiffOptions));
            OnPropertyChanged(nameof(ShowPdfOptions));
            OnPropertyChanged(nameof(ShowQualitySlider));
            OnPropertyChanged(nameof(CanRun));
            OnPropertyChanged(nameof(IsRunButtonEnabled));
            OnPropertyChanged(nameof(ActionButtonLabel));
        }
    }

    // ── Resize inputs ─────────────────────────────────────────────────────────

    private string _widthText = string.Empty;
    public string WidthText
    {
        get => _widthText;
        set
        {
            if (SetProperty(ref _widthText, value))
            {
                if (!_suppressDimensionFlagUpdate)
                    _userHasManuallyChangedDimensions = true;
                OnPropertyChanged(nameof(CanRun));
                OnPropertyChanged(nameof(IsRunButtonEnabled));
            }
        }
    }

    private string _heightText = string.Empty;
    public string HeightText
    {
        get => _heightText;
        set
        {
            if (SetProperty(ref _heightText, value))
            {
                if (!_suppressDimensionFlagUpdate)
                    _userHasManuallyChangedDimensions = true;
                OnPropertyChanged(nameof(CanRun));
                OnPropertyChanged(nameof(IsRunButtonEnabled));
            }
        }
    }

    // ── Strip metadata ────────────────────────────────────────────────────────

    private bool _stripMetadata = true;
    public bool StripMetadata
    {
        get => _stripMetadata;
        set => SetProperty(ref _stripMetadata, value);
    }

    public bool ShowStripMetaOption => HasFiles && !_hasAnyPdfFile;

    // ── Format-specific inline options ────────────────────────────────────────

    public bool ShowGifOptions  => _isCompressEnabled && _detectedMimeType == "image/gif";
    public bool ShowTiffOptions => _isCompressEnabled && _detectedMimeType == "image/tiff";
    public bool ShowPdfOptions  => _isCompressEnabled && _hasAnyPdfFile;

    private bool _optimizeGifFrames = true;
    public bool OptimizeGifFrames
    {
        get => _optimizeGifFrames;
        set => SetProperty(ref _optimizeGifFrames, value);
    }

    private string _tiffCompression = "LZW";
    public string TiffCompression
    {
        get => _tiffCompression;
        set => SetProperty(ref _tiffCompression, value);
    }

    public static IReadOnlyList<string> TiffCompressionOptions { get; } = new[] { "LZW", "ZIP", "JPEG", "None" };

    private bool _linearizePdf = true;
    public bool LinearizePdf
    {
        get => _linearizePdf;
        set => SetProperty(ref _linearizePdf, value);
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
                OnPropertyChanged(nameof(IsJobNotRunning));
                OnPropertyChanged(nameof(CanToggleCompress));
                OnPropertyChanged(nameof(CanToggleResize));
                OnPropertyChanged(nameof(CanToggleConvert));
                OnPropertyChanged(nameof(CanRun));
                OnPropertyChanged(nameof(IsRunButtonEnabled));
                OnPropertyChanged(nameof(ActionButtonLabel));
                OnPropertyChanged(nameof(ShowResetButton));
            }
        }
    }

    public bool IsJobNotRunning => !_isJobRunning;

    private bool _isJobComplete;
    public bool IsJobComplete
    {
        get => _isJobComplete;
        private set
        {
            if (SetProperty(ref _isJobComplete, value))
            {
                OnPropertyChanged(nameof(CanRun));
                OnPropertyChanged(nameof(IsRunButtonEnabled));
            }
        }
    }

    private int _completedCount;
    public int CompletedCount
    {
        get => _completedCount;
        private set
        {
            if (SetProperty(ref _completedCount, value))
                OnPropertyChanged(nameof(ProgressLabel));
        }
    }

    private int _totalCount;
    public int TotalCount
    {
        get => _totalCount;
        private set
        {
            if (SetProperty(ref _totalCount, value))
                OnPropertyChanged(nameof(ProgressLabel));
        }
    }

    public bool ShowSummary => _isSummaryVisible;

    public string ProgressLabel => $"{_completedCount} {_loc.Get("imgtools.progress_of")} {_totalCount}";

    // ── Summary segments ──────────────────────────────────────────────────────

    private string _summaryFilesSegment = string.Empty;
    public string SummaryFilesSegment
    {
        get => _summaryFilesSegment;
        private set => SetProperty(ref _summaryFilesSegment, value);
    }

    private string _summaryNewSizeSegment = string.Empty;
    public string SummaryNewSizeSegment
    {
        get => _summaryNewSizeSegment;
        private set => SetProperty(ref _summaryNewSizeSegment, value);
    }

    private string _summaryOrigSizeSegment = string.Empty;
    public string SummaryOrigSizeSegment
    {
        get => _summaryOrigSizeSegment;
        private set => SetProperty(ref _summaryOrigSizeSegment, value);
    }

    private string _summarySavedSegment = string.Empty;
    public string SummarySavedSegment
    {
        get => _summarySavedSegment;
        private set => SetProperty(ref _summarySavedSegment, value);
    }

    private string _summaryOutputSegment = string.Empty;
    public string SummaryOutputSegment
    {
        get => _summaryOutputSegment;
        private set => SetProperty(ref _summaryOutputSegment, value);
    }

    // Drives which summary segments are shown in XAML (computed before job starts).
    private bool _jobIncludedSizeComparison;
    public bool SummaryShowSizeComparison => _jobIncludedSizeComparison;

    // ── Mixed-type warning ────────────────────────────────────────────────────

    private string _mixedTypeWarning = string.Empty;
    public string MixedTypeWarning
    {
        get => _mixedTypeWarning;
        private set
        {
            if (SetProperty(ref _mixedTypeWarning, value))
                OnPropertyChanged(nameof(HasMixedTypeWarning));
        }
    }
    public bool HasMixedTypeWarning => !string.IsNullOrEmpty(_mixedTypeWarning);

    // ── CanRun ────────────────────────────────────────────────────────────────

    public bool CanRun
    {
        get
        {
            if (Files.Count == 0 || _isJobRunning || _isJobComplete)
                return false;
            if (!_isCompressEnabled && !_isResizeEnabled && !_isConvertEnabled)
                return false;
            if (!_isConvertEnabled && HasMixedTypeWarning)
                return false;
            // If Resize is ON and not blocked by PDF, at least one dimension must be entered.
            if (_isResizeEnabled && !_hasAnyPdfFile)
            {
                bool hasW = uint.TryParse(_widthText, out uint w) && w > 0;
                bool hasH = uint.TryParse(_heightText, out uint h) && h > 0;
                if (!hasW && !hasH)
                    return false;
            }
            return true;
        }
    }

    public bool IsRunButtonEnabled => CanRun || _isJobRunning;

    // ── Action button label ───────────────────────────────────────────────────

    public string ActionButtonLabel
    {
        get
        {
            if (_isJobRunning)
                return _loc.Get("compress.cancel_btn");

            int activeCount = (_isCompressEnabled ? 1 : 0)
                            + (_isResizeEnabled   ? 1 : 0)
                            + (_isConvertEnabled  ? 1 : 0);

            return activeCount switch
            {
                1 when _isCompressEnabled => _loc.Get("imgtools.compress_toggle"),
                1 when _isResizeEnabled   => _loc.Get("imgtools.resize_toggle"),
                1 when _isConvertEnabled  => _loc.Get("imgtools.convert_toggle"),
                _                         => _loc.Get("imgtools.run_btn"),
            };
        }
    }

    // ── Smart defaults flag ───────────────────────────────────────────────────

    private bool _userHasManuallyChangedToggles;

    // ── Dimension auto-population tracking ────────────────────────────────────

    private uint? _originalWidth;
    private uint? _originalHeight;
    private bool _userHasManuallyChangedDimensions;
    private bool _suppressDimensionFlagUpdate;

    // ── Localized labels ──────────────────────────────────────────────────────

    public string DropLabel          => _loc.Get("imgtools.drop");
    public string BrowseLabel        => _loc.Get("imgtools.browse");
    public string ProgressTitleLabel => _loc.Get("imgtools.progress_title");
    public string RemoveFileTooltipLabel => _loc.Get("compress.remove_file");
    public string LevelSmallestLabel => _loc.Get("compress.level_smallest");
    public string LevelLowLabel      => _loc.Get("compress.level_low");
    public string LevelBalancedLabel => _loc.Get("compress.level_balanced");
    public string LevelHighLabel     => _loc.Get("compress.level_high");
    public string LevelBestLabel     => _loc.Get("compress.level_best");
    public string StripMetaLabel     => _loc.Get("imgconv.strip_meta");
    public string AdvGifLabel        => _loc.Get("compress.adv_gif");
    public string AdvOptimizeGifLabel => _loc.Get("compress.adv_optimize_gif");
    public string AdvTiffLabel       => _loc.Get("compress.adv_tiff");
    public string AdvPdfLabel        => _loc.Get("compress.adv_pdf");
    public string AdvLinearizeLabel  => _loc.Get("compress.adv_linearize");
    public string ResetButtonLabel    => _loc.Get("imgtools.reset_btn");
    public string SectionCompressLabel => _loc.Get("imgtools.section_compress");
    public string SectionResizeLabel   => _loc.Get("imgtools.section_resize");
    public string SectionConvertLabel  => _loc.Get("imgtools.section_convert");
    public string CompressToggleLabel  => _loc.Get("imgtools.compress_toggle");
    public string ResizeToggleLabel    => _loc.Get("imgtools.resize_toggle");
    public string ConvertToggleLabel   => _loc.Get("imgtools.convert_toggle");
    public string MixedTypeWarningLabel => _loc.Get("imgtools.mixed_type_warning");
    public string PdfNoResizeTooltip   => _loc.Get("imgtools.pdf_no_resize");
    public string PdfNoConvertTooltip  => _loc.Get("imgtools.pdf_no_convert");
    public string CompressRawTooltip   => _loc.Get("imgtools.raw_no_compress");
    public string WidthHintLabel       => _loc.Get("compress.adv_resize_width_hint");
    public string HeightHintLabel      => _loc.Get("compress.adv_resize_height_hint");
    public string WidthLabelText       => _loc.Get("imgtools.width_label");
    public string HeightLabelText      => _loc.Get("imgtools.height_label");
    public string AddMoreLabel         => _loc.Get("imgtools.add_more");

    public bool ShowResetButton => HasFiles && !_isJobRunning;

    // ── Commands ──────────────────────────────────────────────────────────────

    public ICommand LoadFilesCommand      { get; }
    public ICommand RemoveFileCommand     { get; }
    public ICommand SelectFormatCommand   { get; }
    public ICommand RunCommand            { get; }
    public ICommand DismissSummaryCommand { get; }
    public ICommand OpenHelpCommand       { get; }

    // ── Construction ─────────────────────────────────────────────────────────

    public ImageToolsViewModel(
        IFileTypeService fileTypeService,
        IJobOrchestrationService orchestrator,
        IImageConversionService conversionService,
        ILocalizationService loc)
    {
        _fileTypeService = fileTypeService ?? throw new ArgumentNullException(nameof(fileTypeService));
        _orchestrator = orchestrator ?? throw new ArgumentNullException(nameof(orchestrator));
        _conversionService = conversionService ?? throw new ArgumentNullException(nameof(conversionService));
        _loc = loc ?? throw new ArgumentNullException(nameof(loc));

        LoadFilesCommand    = new RelayCommand(DoLoadFiles, () => !_isJobRunning);
        RemoveFileCommand   = new RelayCommand<string>(DoRemoveFile, _ => !_isJobRunning);
        SelectFormatCommand = new RelayCommand<string>(ext => { if (ext is not null) SetFormat(ext); }, _ => !_isJobRunning);
        RunCommand          = new RelayCommand(DoRun, () => CanRun || _isJobRunning);
        DismissSummaryCommand = new RelayCommand(InternalReset);
        OpenHelpCommand     = new RelayCommand<string>(key =>
        {
            var shell = Application.Current.Windows
                .OfType<Views.ExpandedShellWindow>()
                .FirstOrDefault(w => w.IsVisible)
                ?? (Window)Application.Current.MainWindow!;
            Views.HelpPopup.ShowFor(key!, _loc, shell);
        });

        _orchestrator.Progress += OnOrchestratorProgress;
    }

    // ── AddFiles ──────────────────────────────────────────────────────────────

    private void DoLoadFiles()
    {
        var dlg = new Microsoft.Win32.OpenFileDialog
        {
            Multiselect = true,
            Filter = "Supported files|*.jpg;*.jpeg;*.png;*.webp;*.avif;*.gif;*.bmp;*.tif;*.tiff;*.heic;*.heif;*.ico;*.tga;*.arw;*.cr2;*.cr3;*.dng;*.nef;*.orf;*.raf;*.rw2;*.erf;*.pef;*.x3f;*.pdf|All files (*.*)|*.*",
        };
        if (dlg.ShowDialog() != true)
            return;

        AddFiles(dlg.FileNames);
    }

    public void AddFiles(IReadOnlyList<string> paths)
    {
        CancelPendingAutoReset();
        if (_isJobRunning)
            return;
        if (paths.Count == 0)
            return;
        if (_isSummaryVisible)
        {
            _isSummaryVisible = false;
            OnPropertyChanged(nameof(ShowSummary));
            OnPropertyChanged(nameof(ShowFileList));
        }

        var newItems = new List<CompressionFileItemViewModel>();

        foreach (var path in paths)
        {
            var ext = Path.GetExtension(path);
            if (!_supportedExtensions.Contains(ext))
                continue;

            // Deduplication: replace existing entry with fresh Pending state.
            var existing = Files.FirstOrDefault(f =>
                string.Equals(f.FilePath, path, StringComparison.OrdinalIgnoreCase));
            if (existing is not null)
                Files.Remove(existing);

            var item = new CompressionFileItemViewModel
            {
                FilePath = path,
                FileName = Path.GetFileName(path),
                FileSizeBytes = GetFileSizeBytes(path),
                Status = CompressionFileStatus.Pending,
            };
            Files.Add(item);
            newItems.Add(item);
        }

        // Read dimensions asynchronously — Ping reads only the image header.
        foreach (var item in newItems)
        {
            var capturedItem = item;
            _ = Task.Run(() =>
            {
                var dims = ImageDimensionReader.TryRead(capturedItem.FilePath);
                System.Windows.Application.Current?.Dispatcher.InvokeAsync(() =>
                {
                    if (dims.HasValue)
                        capturedItem.Dimensions = $"{dims.Value.Width} × {dims.Value.Height}";
                    TryAutoPopulateDimensions();
                });
            });
        }

        RefreshDetectedType();
        ApplySmartDefaults();
        ReEvaluateMixedTypeWarning();

        OnPropertyChanged(nameof(HasFiles));
        OnPropertyChanged(nameof(ShowFileList));
        OnPropertyChanged(nameof(ShowStripMetaOption));
        OnPropertyChanged(nameof(ShowResetButton));
        OnPropertyChanged(nameof(CanRun));
        OnPropertyChanged(nameof(IsRunButtonEnabled));
        FireToggleAndSectionProps();
    }

    // ── Remove file ───────────────────────────────────────────────────────────

    private void DoRemoveFile(string? filePath)
    {
        if (filePath is null) return;
        if (_isJobRunning) return;

        var item = Files.FirstOrDefault(f => f.FilePath == filePath);
        if (item is null) return;

        Files.Remove(item);
        RefreshDetectedType();
        ReEvaluateMixedTypeWarning();
        TryAutoPopulateDimensions();

        OnPropertyChanged(nameof(HasFiles));
        OnPropertyChanged(nameof(ShowFileList));
        OnPropertyChanged(nameof(ShowStripMetaOption));
        OnPropertyChanged(nameof(ShowResetButton));
        OnPropertyChanged(nameof(CanRun));
        OnPropertyChanged(nameof(IsRunButtonEnabled));
        FireToggleAndSectionProps();
    }

    // ── Dimension auto-population ─────────────────────────────────────────────

    /// <summary>
    /// If all files have matching pixel dimensions and the user has not manually edited the
    /// dimension fields, populate Width/Height and lock the aspect ratio.
    /// Called from the async dimension-read callback in AddFiles.
    /// </summary>
    private void TryAutoPopulateDimensions()
    {
        if (_userHasManuallyChangedDimensions)
            return;

        // All files must have dimensions loaded (every async read finished).
        if (Files.Count == 0 || Files.Any(f => !f.HasDimensions))
            return;

        // All must share the same dimensions.
        var first = Files[0].Dimensions;
        if (Files.Any(f => f.Dimensions != first))
        {
            // Mixed dimensions: clear auto-populated fields if they were previously set.
            if (_originalWidth.HasValue)
            {
                _originalWidth  = null;
                _originalHeight = null;
                _suppressDimensionFlagUpdate = true;
                WidthText  = string.Empty;
                HeightText = string.Empty;
                _suppressDimensionFlagUpdate = false;
            }
            return;
        }

        // Parse "W × H" format written by AddFiles.
        var parts = first.Split('×', StringSplitOptions.TrimEntries);
        if (parts.Length != 2
            || !uint.TryParse(parts[0], out uint w) || w == 0
            || !uint.TryParse(parts[1], out uint h) || h == 0)
            return;

        _originalWidth  = w;
        _originalHeight = h;
        RecomputeProjectedDimensions();
    }

    /// <summary>
    /// Recomputes Width/Height from the stored original dimensions using the current
    /// quality-level percentage. Called on first populate and on every level change.
    /// No-ops if original dimensions are not yet known or user has manually edited the fields.
    /// </summary>
    private void RecomputeProjectedDimensions()
    {
        if (_userHasManuallyChangedDimensions) return;
        if (_originalWidth is null || _originalHeight is null) return;

        double pct = CompressionProfiles.ResizeDefaultPercent[Math.Clamp(_qualityLevel, 0, 4)];
        uint projW = Math.Max(1u, (uint)Math.Round(_originalWidth.Value  * pct));
        uint projH = Math.Max(1u, (uint)Math.Round(_originalHeight.Value * pct));

        _suppressDimensionFlagUpdate = true;
        WidthText  = projW.ToString();
        HeightText = projH.ToString();
        _suppressDimensionFlagUpdate = false;
    }

    // ── Smart defaults ────────────────────────────────────────────────────────

    private void ApplySmartDefaults()    {
        if (_userHasManuallyChangedToggles)
            return;

        bool changed = false;
        bool newCompress = _isCompressEnabled;
        bool newResize = _isResizeEnabled;
        bool newConvert = _isConvertEnabled;
        string newFormat = _selectedFormatExt;

        if (_hasAnyPdfFile)
        {
            newCompress = true;
            newResize   = false;
            newConvert  = false;
        }
        else if (_detectedCategory == FileCategory.Raw)
        {
            // RAW: convert to JPEG only; no compression (RAW cannot be compressed),
            // no resize (keep original resolution).
            newCompress = false;
            newConvert  = true;
            newResize   = false;
            newFormat   = "jpg";
        }
        else if (_detectedMimeType == "image/heif")
        {
            // HEIC/HEIF: always convert to JPEG for compatibility.
            newCompress = true;
            newConvert  = true;
            newResize   = true;
            newFormat   = "jpg";
        }
        else if (_detectedMimeType == "image/gif")
        {
            // GIF: Compress only at ALL quality levels (animation semantics must be preserved).
            newCompress = true;
            newResize   = false;
            newConvert  = false;
        }
        else if (_detectedMimeType is "image/webp" or "image/avif")
        {
            // Modern formats: no auto-convert (re-encoding would be lossy with no benefit).
            newCompress = true;
            newResize   = true;
            newConvert  = false;
        }
        else
        {
            // Standard image types: jpeg, png, tiff, bmp, and any other.
            // Quality level controls degree of compression/resize, not which operations are active.
            // PNG may have transparency; converting to JPEG removes the alpha channel
            // (replaced with the canvas color by ImageMagick). This is a known trade-off.
            newCompress = true;
            newResize   = true;
            newConvert  = true;
            newFormat   = "jpg";
        }

        // Apply changes without triggering _userHasManuallyChangedToggles — suppress the
        // normal setter path by updating backing fields and firing manually.
        if (_isCompressEnabled != newCompress) { _isCompressEnabled = newCompress; changed = true; }
        if (_isResizeEnabled   != newResize)   { _isResizeEnabled   = newResize;   changed = true; }
        if (_isConvertEnabled  != newConvert)  { _isConvertEnabled  = newConvert;  changed = true; }
        if (_selectedFormatExt != newFormat)
        {
            _selectedFormatExt = newFormat;
            changed = true;
            OnPropertyChanged(nameof(SelectedFormatExt));
            OnPropertyChanged(nameof(IsFormatJpeg));
            OnPropertyChanged(nameof(IsFormatPng));
            OnPropertyChanged(nameof(IsFormatWebp));
            OnPropertyChanged(nameof(IsFormatAvif));
            OnPropertyChanged(nameof(IsFormatGif));
            OnPropertyChanged(nameof(IsFormatBmp));
            OnPropertyChanged(nameof(IsFormatTiff));
            OnPropertyChanged(nameof(IsFormatIco));
            OnPropertyChanged(nameof(IsFormatTga));
        }

        if (changed)
        {
            OnPropertyChanged(nameof(IsCompressEnabled));
            OnPropertyChanged(nameof(IsResizeEnabled));
            OnPropertyChanged(nameof(IsConvertEnabled));
            OnPropertyChanged(nameof(ShowQualitySlider));
            OnPropertyChanged(nameof(ActionButtonLabel));
            OnPropertyChanged(nameof(CanRun));
            OnPropertyChanged(nameof(IsRunButtonEnabled));
            FireToggleAndSectionProps();
        }

        // Smart defaults may have re-enabled compress (e.g. newCompress=true) while
        // a no-compression output format is still selected. Re-run the format constraint
        // to enforce correct state regardless of what smart defaults decided.
        EnforceFormatConstraints();
    }

    // ── RefreshDetectedType ───────────────────────────────────────────────────

    private void RefreshDetectedType()
    {
        _hasAnyPdfFile = Files.Any(f =>
            string.Equals(Path.GetExtension(f.FilePath), ".pdf", StringComparison.OrdinalIgnoreCase));

        if (Files.Count == 0)
        {
            _detectedMimeType = string.Empty;
            _detectedCategory = FileCategory.Unsupported;
            _hasMultipleMimeTypes = false;
        }
        else
        {
            var mimes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var f in Files)
            {
                var ext = Path.GetExtension(f.FilePath);
                var mime = _fileTypeService.GetMimeType(ext);
                if (mime is not null)
                    mimes.Add(mime);
            }

            _hasMultipleMimeTypes = mimes.Count > 1;

            if (mimes.Count == 1)
            {
                _detectedMimeType = mimes.First();
                _detectedCategory = _fileTypeService.GetCategory(_detectedMimeType);
            }
            else
            {
                // Mixed types or unknown: cannot represent a single type.
                _detectedMimeType = string.Empty;
                _detectedCategory = FileCategory.Unsupported;
            }
        }

        OnPropertyChanged(nameof(HasAnyPdfFile));
        OnPropertyChanged(nameof(IsRawOnlyFiles));
        OnPropertyChanged(nameof(CanToggleCompress));
        OnPropertyChanged(nameof(CanToggleResize));
        OnPropertyChanged(nameof(CanToggleConvert));
        OnPropertyChanged(nameof(ShowGifOptions));
        OnPropertyChanged(nameof(ShowTiffOptions));
        OnPropertyChanged(nameof(ShowPdfOptions));
        OnPropertyChanged(nameof(ShowResizeSection));
        OnPropertyChanged(nameof(ShowConvertSection));
        OnPropertyChanged(nameof(ShowStripMetaOption));
    }

    // ── Mixed-type warning ────────────────────────────────────────────────────

    private void ReEvaluateMixedTypeWarning()
    {
        if (!_hasMultipleMimeTypes || _isConvertEnabled)
        {
            MixedTypeWarning = string.Empty;
        }
        else
        {
            MixedTypeWarning = _loc.Get("imgtools.mixed_type_warning");
        }
    }

    // ── DoRun ─────────────────────────────────────────────────────────────────

    private async void DoRun()
    {
        if (_isJobRunning)
        {
            _orchestrator.CancelJob();
            return;
        }

        if (!CanRun)
            return;

        // Capture routing decision before any await so toggle changes don't affect it.
        bool useConversionPipeline = _isConvertEnabled;
        _jobIncludedSizeComparison = _isCompressEnabled || _isResizeEnabled;

        // Output dialog.
        string? outputDir;
        string? singleOutputPath;
        if (!PickOutputPaths(useConversionPipeline, out outputDir, out singleOutputPath))
            return;

        // Build jobs.
        if (useConversionPipeline)
        {
            var convJobs = BuildConversionJobs(outputDir, singleOutputPath);
            if (convJobs.Count == 0)
                return;

            IsJobRunning = true;
            IsJobComplete = false;
            CompletedCount = 0;
            TotalCount = convJobs.Count;

            foreach (var f in Files)
            {
                // PDF files were marked Error in BuildConversionJobs; don't overwrite them.
                if (!string.Equals(Path.GetExtension(f.FilePath), ".pdf", StringComparison.OrdinalIgnoreCase))
                    f.Status = CompressionFileStatus.Pending;
            }

            await _orchestrator.StartConversionJobAsync(convJobs);

            IsJobRunning = false;
            IsJobComplete = true;

            ApplyConversionJobResults(convJobs);
            BuildSummary(convJobs.Select(j => (j.InputPath, j.OutputPath)).ToList());
            bool hasErrors = Files.Any(f => f.IsError);
            InternalReset();
            _isSummaryVisible = true;
            OnPropertyChanged(nameof(ShowSummary));
            OnPropertyChanged(nameof(ShowFileList));
            ScheduleAutoReset(hasErrors);
        }
        else
        {
            var compJobs = BuildCompressionJobs(outputDir, singleOutputPath);
            if (compJobs.Count == 0)
                return;

            IsJobRunning = true;
            IsJobComplete = false;
            CompletedCount = 0;
            TotalCount = compJobs.Count;

            foreach (var f in Files)
                f.Status = CompressionFileStatus.Pending;

            await _orchestrator.StartJobAsync(compJobs);

            IsJobRunning = false;
            IsJobComplete = true;

            ApplyCompressionJobResults(compJobs);
            BuildSummary(compJobs.Select(j => (j.InputPath, j.OutputPath)).ToList());
            bool hasErrors = Files.Any(f => f.IsError);
            InternalReset();
            _isSummaryVisible = true;
            OnPropertyChanged(nameof(ShowSummary));
            OnPropertyChanged(nameof(ShowFileList));
            ScheduleAutoReset(hasErrors);
        }
    }

    private bool PickOutputPaths(bool isConvert, out string? outputDir, out string? singleOutputPath)
    {
        outputDir = null;
        singleOutputPath = null;

        // Files eligible for output dialog (exclude PDF when using conversion pipeline).
        var eligibleFiles = isConvert
            ? Files.Where(f => !string.Equals(Path.GetExtension(f.FilePath), ".pdf", StringComparison.OrdinalIgnoreCase)).ToList()
            : Files.ToList();

        if (eligibleFiles.Count == 0)
            return false;

        var targetExt = isConvert ? _selectedFormatExt : Path.GetExtension(eligibleFiles[0].FilePath).TrimStart('.');

        if (eligibleFiles.Count == 1)
        {
            var inputPath = eligibleFiles[0].FilePath;
            var suggestedName = ProcessedOutputNaming.BuildOutputPath(inputPath,
                Path.GetDirectoryName(inputPath) ?? string.Empty, targetExt);

            var dlg = new Microsoft.Win32.SaveFileDialog
            {
                FileName = Path.GetFileName(suggestedName),
                InitialDirectory = Path.GetDirectoryName(inputPath) ?? string.Empty,
                Filter = $"{targetExt.ToUpperInvariant()} files (*.{targetExt})|*.{targetExt}|All files (*.*)|*.*",
            };
            if (dlg.ShowDialog() != true)
                return false;

            singleOutputPath = dlg.FileName;
            return true;
        }
        else
        {
            var dlg = new Microsoft.Win32.OpenFolderDialog
            {
                Title = "Select Output Folder",
            };
            if (dlg.ShowDialog() != true)
                return false;

            outputDir = dlg.FolderName;
            return true;
        }
    }

    private List<ConversionJobDescriptor> BuildConversionJobs(string? outputDir, string? singleOutputPath)
    {
        var jobs = new List<ConversionJobDescriptor>();
        var targetExt = _selectedFormatExt;
        int qualityLevel = _isCompressEnabled ? _qualityLevel : 4; // 4 = max quality when Compress is OFF
        uint? width  = (_isResizeEnabled && !_hasAnyPdfFile) ? ParsePositiveUInt(_widthText)  : null;
        uint? height = (_isResizeEnabled && !_hasAnyPdfFile) ? ParsePositiveUInt(_heightText) : null;

        bool isSingleFile = singleOutputPath is not null;

        for (int i = 0; i < Files.Count; i++)
        {
            var f = Files[i];
            var ext = Path.GetExtension(f.FilePath);

            // Skip PDF files in conversion pipeline — mark them as Error immediately.
            if (string.Equals(ext, ".pdf", StringComparison.OrdinalIgnoreCase))
            {
                f.Status = CompressionFileStatus.Error;
                continue;
            }

            string outPath;
            if (isSingleFile)
            {
                outPath = singleOutputPath!;
            }
            else
            {
                outPath = ProcessedOutputNaming.BuildOutputPath(f.FilePath, outputDir!, targetExt);
            }

            jobs.Add(new ConversionJobDescriptor
            {
                InputPath      = f.FilePath,
                OutputPath     = outPath,
                TargetExtension = targetExt,
                QualityLevel   = qualityLevel,
                StripMetadata  = _stripMetadata,
                TargetWidth    = width,
                TargetHeight   = height,
            });
        }

        return jobs;
    }

    private List<CompressionJob> BuildCompressionJobs(string? outputDir, string? singleOutputPath)
    {
        var jobs = new List<CompressionJob>();
        uint? width  = (_isResizeEnabled && !_hasAnyPdfFile) ? ParsePositiveUInt(_widthText)  : null;
        uint? height = (_isResizeEnabled && !_hasAnyPdfFile) ? ParsePositiveUInt(_heightText) : null;

        var settings = new CompressionSettings
        {
            CompressionLevel = _qualityLevel,
            ResizeWidth  = width,
            ResizeHeight = height,
            Advanced = new AdvancedCompressionSettings
            {
                StripMetadata    = _stripMetadata,
                OptimizeGifFrames = _optimizeGifFrames,
                TiffCompression  = _tiffCompression,
                LinearizePdf     = _linearizePdf,
            },
        };

        for (int i = 0; i < Files.Count; i++)
        {
            var f = Files[i];
            var ext = Path.GetExtension(f.FilePath);
            var mime = _fileTypeService.GetMimeType(ext) ?? string.Empty;
            var cat  = _fileTypeService.GetCategory(mime);

            string outPath;
            if (singleOutputPath is not null)
            {
                outPath = singleOutputPath;
            }
            else
            {
                // Keep the same extension for compression (no conversion in this path).
                outPath = ProcessedOutputNaming.BuildOutputPath(f.FilePath, outputDir!, ext.TrimStart('.'));
            }

            jobs.Add(new CompressionJob
            {
                InputPath  = f.FilePath,
                OutputPath = outPath,
                Settings   = settings,
                Category   = cat,
                MimeType   = mime,
            });
        }

        return jobs;
    }

    // ── Job result application ────────────────────────────────────────────────

    private void ApplyConversionJobResults(IReadOnlyList<ConversionJobDescriptor> jobs)
    {
        foreach (var job in jobs)
        {
            var item = Files.FirstOrDefault(f => f.FilePath == job.InputPath);
            if (item is null) continue;
            item.Status = File.Exists(job.OutputPath)
                ? CompressionFileStatus.Done
                : CompressionFileStatus.Error;
            item.OutputSizeBytes = item.IsDone ? GetFileSizeBytes(job.OutputPath) : 0;
            item.NotifyStatus();
        }
    }

    private void ApplyCompressionJobResults(IReadOnlyList<CompressionJob> jobs)
    {
        foreach (var job in jobs)
        {
            var item = Files.FirstOrDefault(f => f.FilePath == job.InputPath);
            if (item is null) continue;
            item.Status = File.Exists(job.OutputPath)
                ? CompressionFileStatus.Done
                : CompressionFileStatus.Error;
            item.OutputSizeBytes = item.IsDone ? GetFileSizeBytes(job.OutputPath) : 0;
            item.NotifyStatus();
        }
    }

    // ── Summary building ──────────────────────────────────────────────────────

    private void BuildSummary(IReadOnlyList<(string input, string output)> paths)
    {
        int doneCount   = Files.Count(f => f.IsDone);
        int failedCount = Files.Count(f => f.IsError);

        SummaryFilesSegment = failedCount == 0
            ? (doneCount == 1
                ? _loc.Get("compress.summary_seg_files_one")
                : string.Format(_loc.Get("compress.summary_seg_files_many"), doneCount))
            : string.Format(_loc.Get("compress.summary_seg_partial"), doneCount, failedCount);

        if (_jobIncludedSizeComparison)
        {
            long totalIn  = paths.Sum(p => GetFileSizeBytes(p.input));
            long totalOut = paths.Sum(p => GetFileSizeBytes(p.output));
            long saved    = Math.Max(0, totalIn - totalOut);

            SummaryOrigSizeSegment = string.Format(_loc.Get("compress.summary_seg_orig_size"), FormatBytes(totalIn));
            SummaryNewSizeSegment  = string.Format(_loc.Get("compress.summary_seg_new_size"),  FormatBytes(totalOut));
            SummarySavedSegment    = string.Format(_loc.Get("compress.summary_seg_saved"),     FormatBytes(saved));
            SummaryOutputSegment   = string.Empty;
        }
        else
        {
            // Convert-only: no meaningful size comparison.
            long totalOut = paths.Sum(p => GetFileSizeBytes(p.output));
            SummaryOutputSegment   = string.Format(_loc.Get("imgtools.summary_output_size"), FormatBytes(totalOut));
            SummaryOrigSizeSegment = string.Empty;
            SummaryNewSizeSegment  = string.Empty;
            SummarySavedSegment    = string.Empty;
        }

        OnPropertyChanged(nameof(SummaryShowSizeComparison));
    }

    // ── Progress handler ──────────────────────────────────────────────────────

    private void OnOrchestratorProgress(object? sender, JobProgressState state)
    {
        // Multiple VMs subscribe to the shared orchestrator; only the VM that
        // started the current job should respond.
        if (!_isJobRunning)
            return;

        Application.Current?.Dispatcher.InvokeAsync(() =>
        {
            CompletedCount = state.CompletedCount;
            TotalCount = state.TotalCount;
        });
    }

    // ── InternalReset ─────────────────────────────────────────────────────────

    private void InternalReset()
    {
        CancelPendingAutoReset();
        _isSummaryVisible = false;
        Files.Clear();
        _detectedMimeType = string.Empty;
        _detectedCategory = FileCategory.Unsupported;
        _hasMultipleMimeTypes = false;
        _hasAnyPdfFile = false;
        _userHasManuallyChangedToggles = false;
        _isCompressEnabled = false;
        _isResizeEnabled   = false;
        _isConvertEnabled  = false;
        _jobIncludedSizeComparison = false;
        MixedTypeWarning = string.Empty;
        IsJobRunning  = false;
        IsJobComplete = false;
        CompletedCount = 0;
        TotalCount     = 0;
        _widthText  = string.Empty;
        _heightText = string.Empty;
        _originalWidth  = null;
        _originalHeight = null;
        _userHasManuallyChangedDimensions = false;
        _compressDisabledByFormat = false;
        OnPropertyChanged(nameof(WidthText));
        OnPropertyChanged(nameof(HeightText));

        OnPropertyChanged(nameof(HasFiles));
        OnPropertyChanged(nameof(ShowFileList));
        OnPropertyChanged(nameof(ShowSummary));
        OnPropertyChanged(nameof(ShowStripMetaOption));
        OnPropertyChanged(nameof(ShowResetButton));
        OnPropertyChanged(nameof(CanRun));
        OnPropertyChanged(nameof(IsRunButtonEnabled));
        OnPropertyChanged(nameof(ActionButtonLabel));
        OnPropertyChanged(nameof(ShowQualitySlider));
        FireToggleAndSectionProps();
    }

    // ── Auto-reset timer ─────────────────────────────────────────────────────

    private void ScheduleAutoReset(bool hasErrors)
    {
        CancelPendingAutoReset();
        _resetTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(hasErrors ? 8 : 3) };
        _resetTimer.Tick += (_, _) => { CancelPendingAutoReset(); InternalReset(); };
        _resetTimer.Start();
    }

    private void CancelPendingAutoReset()
    {
        _resetTimer?.Stop();
        _resetTimer = null;
    }

    // ── FireToggleAndSectionProps ─────────────────────────────────────────────

    /// <summary>
    /// Fires all toggle, CanToggle, section visibility, and format-specific option
    /// properties in one place to avoid scattered OnPropertyChanged calls.
    /// </summary>
    private void FireToggleAndSectionProps()
    {
        OnPropertyChanged(nameof(IsCompressEnabled));
        OnPropertyChanged(nameof(IsResizeEnabled));
        OnPropertyChanged(nameof(IsConvertEnabled));
        OnPropertyChanged(nameof(CanToggleCompress));
        OnPropertyChanged(nameof(CanToggleResize));
        OnPropertyChanged(nameof(CanToggleConvert));
        OnPropertyChanged(nameof(IsRawOnlyFiles));
        OnPropertyChanged(nameof(HasAnyPdfFile));
        OnPropertyChanged(nameof(ShowCompressSection));
        OnPropertyChanged(nameof(ShowResizeSection));
        OnPropertyChanged(nameof(ShowConvertSection));
        OnPropertyChanged(nameof(ShowGifOptions));
        OnPropertyChanged(nameof(ShowTiffOptions));
        OnPropertyChanged(nameof(ShowPdfOptions));
        OnPropertyChanged(nameof(ShowQualitySlider));
    }

    // ── Language support ──────────────────────────────────────────────────────

    public void OnLanguageChanged()
    {
        OnPropertyChanged(nameof(DropLabel));
        OnPropertyChanged(nameof(BrowseLabel));
        OnPropertyChanged(nameof(ProgressTitleLabel));
        OnPropertyChanged(nameof(ProgressLabel));
        OnPropertyChanged(nameof(RemoveFileTooltipLabel));
        OnPropertyChanged(nameof(LevelSmallestLabel));
        OnPropertyChanged(nameof(LevelLowLabel));
        OnPropertyChanged(nameof(LevelBalancedLabel));
        OnPropertyChanged(nameof(LevelHighLabel));
        OnPropertyChanged(nameof(LevelBestLabel));
        OnPropertyChanged(nameof(StripMetaLabel));
        OnPropertyChanged(nameof(AdvGifLabel));
        OnPropertyChanged(nameof(AdvOptimizeGifLabel));
        OnPropertyChanged(nameof(AdvTiffLabel));
        OnPropertyChanged(nameof(AdvPdfLabel));
        OnPropertyChanged(nameof(AdvLinearizeLabel));
        OnPropertyChanged(nameof(ResetButtonLabel));
        OnPropertyChanged(nameof(SectionCompressLabel));
        OnPropertyChanged(nameof(SectionResizeLabel));
        OnPropertyChanged(nameof(SectionConvertLabel));
        OnPropertyChanged(nameof(CompressToggleLabel));
        OnPropertyChanged(nameof(ResizeToggleLabel));
        OnPropertyChanged(nameof(ConvertToggleLabel));
        OnPropertyChanged(nameof(MixedTypeWarningLabel));
        OnPropertyChanged(nameof(PdfNoResizeTooltip));
        OnPropertyChanged(nameof(PdfNoConvertTooltip));
        OnPropertyChanged(nameof(CompressRawTooltip));
        OnPropertyChanged(nameof(WidthHintLabel));
        OnPropertyChanged(nameof(HeightHintLabel));
        OnPropertyChanged(nameof(WidthLabelText));
        OnPropertyChanged(nameof(HeightLabelText));
        OnPropertyChanged(nameof(AddMoreLabel));
        OnPropertyChanged(nameof(ActionButtonLabel));
    }

    // ── Static helpers ────────────────────────────────────────────────────────

    private static uint? ParsePositiveUInt(string text)
    {
        if (uint.TryParse(text, out var v) && v > 0) return v;
        return null;
    }
}
