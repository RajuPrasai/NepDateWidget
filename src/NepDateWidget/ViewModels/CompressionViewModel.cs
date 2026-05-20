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
/// Manages the Compression sub-view state and orchestrates compression jobs.
/// Singleton - survives shell expand/collapse cycles.
/// </summary>
public sealed class CompressionViewModel : ViewModelBase
{
    private readonly IFileTypeService _fileTypeService;
    private readonly IJobOrchestrationService _orchestrator;
    private readonly ILocalizationService _loc;
    private DispatcherTimer? _resetTimer;

    // ── File list ────────────────────────────────────────────────────────────

    public ObservableCollection<CompressionFileItemViewModel> Files { get; } = new();

    private string _detectedMimeType = string.Empty;
    public string DetectedMimeType => _detectedMimeType;

    private FileCategory _detectedCategory = FileCategory.Unsupported;
    public FileCategory DetectedCategory => _detectedCategory;

    // ── Compression level (0..4 from smallest output to best quality) ───────

    private int _compressionLevel = 1;
    public int CompressionLevel
    {
        get => _compressionLevel;
        set => SetProperty(ref _compressionLevel, Math.Clamp(value, 0, 4));
    }

    // ── Advanced panel ───────────────────────────────────────────────────────

    private bool _isAdvancedPanelOpen;
    public bool IsAdvancedPanelOpen
    {
        get => _isAdvancedPanelOpen;
        set => SetProperty(ref _isAdvancedPanelOpen, value);
    }

    public bool CanOpenAdvancedPanel => Files.Count > 0;

    // Optional resize in compression flow (advanced panel).
    private string _resizeWidthText = string.Empty;
    public string ResizeWidthText
    {
        get => _resizeWidthText;
        set => SetProperty(ref _resizeWidthText, value);
    }

    private string _resizeHeightText = string.Empty;
    public string ResizeHeightText
    {
        get => _resizeHeightText;
        set => SetProperty(ref _resizeHeightText, value);
    }

    // ── Advanced settings model ──────────────────────────────────────────────

    private AdvancedCompressionSettings _advancedSettings = new();
    public AdvancedCompressionSettings AdvancedSettings
    {
        get => _advancedSettings;
        private set => SetProperty(ref _advancedSettings, value);
    }

    // Advanced panel toggles - bound individually so view can update live.

    private bool _stripMetadata = true;
    public bool StripMetadata
    {
        get => _stripMetadata;
        set
        {
            if (SetProperty(ref _stripMetadata, value))
            {
                _advancedSettings.StripMetadata = value;
            }
        }
    }

    private bool _convertToWebP;
    public bool ConvertToWebP
    {
        get => _convertToWebP;
        set
        {
            if (SetProperty(ref _convertToWebP, value))
            {
                _advancedSettings.ConvertToWebP = value;
                OnPropertyChanged(nameof(ShowLosslessWebP));
            }
        }
    }

    private bool _losslessWebP;
    public bool LosslessWebP
    {
        get => _losslessWebP;
        set
        {
            if (SetProperty(ref _losslessWebP, value))
            {
                _advancedSettings.LosslessWebP = value;
            }
        }
    }

    public bool ShowLosslessWebP => string.Equals(_detectedMimeType, "image/webp", StringComparison.OrdinalIgnoreCase)
                                    || (_detectedMimeType == "image/png" && _convertToWebP);
    private bool _optimizeGifFrames = true;
    public bool OptimizeGifFrames
    {
        get => _optimizeGifFrames;
        set
        {
            if (SetProperty(ref _optimizeGifFrames, value))
            {
                _advancedSettings.OptimizeGifFrames = value;
            }
        }
    }

    private string _tiffCompression = "LZW";
    public string TiffCompression
    {
        get => _tiffCompression;
        set
        {
            if (SetProperty(ref _tiffCompression, value))
            {
                _advancedSettings.TiffCompression = value;
            }
        }
    }

    private bool _linearizePdf = true;
    public bool LinearizePdf
    {
        get => _linearizePdf;
        set
        {
            if (SetProperty(ref _linearizePdf, value))
            {
                _advancedSettings.LinearizePdf = value;
            }
        }
    }

    // ── Job state ────────────────────────────────────────────────────────────

    private bool _isJobRunning;
    public bool IsJobRunning
    {
        get => _isJobRunning;
        private set
        {
            if (SetProperty(ref _isJobRunning, value))
            {
                OnPropertyChanged(nameof(CanCompress));
                OnPropertyChanged(nameof(CompressButtonLabel));
            }
        }
    }

    private bool _isJobComplete;
    public bool IsJobComplete
    {
        get => _isJobComplete;
        private set
        {
            if (SetProperty(ref _isJobComplete, value))
            {
                OnPropertyChanged(nameof(ShowSummary));
                OnPropertyChanged(nameof(ShowFileList));
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
            {
                OnPropertyChanged(nameof(ProgressLabel));
            }
        }
    }

    private int _totalCount;
    public int TotalCount
    {
        get => _totalCount;
        private set
        {
            if (SetProperty(ref _totalCount, value))
            {
                OnPropertyChanged(nameof(ProgressLabel));
            }
        }
    }

    public string ProgressLabel => $"{_completedCount} {_loc.Get("compress.progress_of")} {_totalCount}";

    private string _jobSummary = string.Empty;
    public string JobSummary
    {
        get => _jobSummary;
        private set => SetProperty(ref _jobSummary, value);
    }

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

    public bool ShowSummary => _isJobComplete;

    // ── Mixed-type warning ───────────────────────────────────────────────────

    private string _mixedTypeWarning = string.Empty;
    public string MixedTypeWarning
    {
        get => _mixedTypeWarning;
        private set
        {
            if (SetProperty(ref _mixedTypeWarning, value))
            {
                OnPropertyChanged(nameof(HasMixedTypeWarning));
            }
        }
    }
    public bool HasMixedTypeWarning => !string.IsNullOrEmpty(_mixedTypeWarning);

    // ── Labels ───────────────────────────────────────────────────────────────

    public bool IsMimeJpeg => _detectedMimeType == "image/jpeg";
    public bool IsMimePng => _detectedMimeType == "image/png";
    public bool IsMimeWebP => _detectedMimeType == "image/webp";
    public bool IsMimeGif => _detectedMimeType == "image/gif";
    public bool IsMimeTiff => _detectedMimeType == "image/tiff";
    public bool IsMimeBmp => _detectedMimeType == "image/bmp";
    public bool IsMimeHeif => _detectedMimeType == "image/heif";
    public bool IsMimeAvif => _detectedMimeType == "image/avif";
    public bool IsMimePdf => _detectedMimeType == "application/pdf";
    public bool IsImageType => _detectedCategory == FileCategory.Image;
    // Convenience flag: formats that support WebP conversion
    public bool IsMimeJpegOrPng => IsMimeJpeg || IsMimePng;
    // True when there is at least one file loaded (drives drop-zone vs file-list visibility)
    public bool HasFiles => Files.Count > 0;
    // Hides the file list once the job completes and the summary banner is shown
    public bool ShowFileList => HasFiles && !ShowSummary;

    // TIFF compression algorithm choices presented in the advanced panel
    public static IReadOnlyList<string> TiffCompressionOptions { get; } =
        new[] { "LZW", "ZIP", "JPEG", "None" };

    public string CompressButtonLabel => _isJobRunning ? _loc.Get("compress.cancel_btn") : _loc.Get("compress.compress_btn");
    public bool CanCompress => Files.Count > 0 && !_isJobRunning && !_isJobComplete && !HasMixedTypeWarning;

    // ── Localized labels ─────────────────────────────────────────────────────

    public string DropLabel => _loc.Get("compress.drop_images_pdf");
    public string BrowseLabel => _loc.Get("compress.browse");
    public string CancelLabel => _loc.Get("compress.cancel_btn");
    public string ProgressTitleLabel => _loc.Get("compress.compressing");
    public string LevelSectionLabel => _loc.Get("compress.level_section");
    public string LevelSmallestLabel => _loc.Get("compress.level_smallest");
    public string LevelLowLabel => _loc.Get("compress.level_low");
    public string LevelBalancedLabel => _loc.Get("compress.level_balanced");
    public string LevelHighLabel => _loc.Get("compress.level_high");
    public string LevelBestLabel => _loc.Get("compress.level_best");
    public string AdvancedBtnLabel => _loc.Get("compress.advanced_btn");
    public string AdvImageLabel => _loc.Get("compress.adv_image");
    public string AdvOptionalResizeLabel => _loc.Get("compress.adv_optional_resize");
    public string AdvStripMetaLabel => _loc.Get("compress.adv_strip_meta");
    public string AdvToWebPLabel => _loc.Get("compress.adv_to_webp");
    public string AdvLosslessWebPLabel => _loc.Get("compress.adv_lossless_webp");
    public string AdvGifLabel => _loc.Get("compress.adv_gif");
    public string AdvOptimizeGifLabel => _loc.Get("compress.adv_optimize_gif");
    public string AdvTiffLabel => _loc.Get("compress.adv_tiff");
    public string AdvPdfLabel => _loc.Get("compress.adv_pdf");
    public string AdvLinearizeLabel => _loc.Get("compress.adv_linearize");
    public string AdvResizeWidthHint => _loc.Get("compress.adv_resize_width_hint");
    public string AdvResizeHeightHint => _loc.Get("compress.adv_resize_height_hint");
    public string RemoveFileTooltipLabel => _loc.Get("compress.remove_file");
    public string AdvResizeWidthTooltip => _loc.Get("compress.tooltip_resize_width");
    public string AdvResizeHeightTooltip => _loc.Get("compress.tooltip_resize_height");
    public string AdvAspectHintLabel => _loc.Get("compress.adv_aspect_hint");

    // ── Commands ─────────────────────────────────────────────────────────────

    public ICommand LoadFilesCommand { get; }
    public ICommand RemoveFileCommand { get; }
    public ICommand CompressCommand { get; }
    public ICommand CancelCommand { get; }
    public ICommand ToggleAdvancedPanelCommand { get; }
    public ICommand OpenHelpCommand { get; }
    public ICommand DismissSummaryCommand { get; }

    // ── Construction ─────────────────────────────────────────────────────────

    public CompressionViewModel(IFileTypeService fileTypeService, IJobOrchestrationService orchestrator, ILocalizationService loc)
    {
        _fileTypeService = fileTypeService ?? throw new ArgumentNullException(nameof(fileTypeService));
        _orchestrator = orchestrator ?? throw new ArgumentNullException(nameof(orchestrator));
        _loc = loc ?? throw new ArgumentNullException(nameof(loc));

        LoadFilesCommand = new RelayCommand(DoLoadFiles);
        RemoveFileCommand = new RelayCommand<string>(DoRemoveFile);
        CompressCommand = new RelayCommand(DoCompress, () => CanCompress);
        CancelCommand = new RelayCommand(DoCancel, () => _isJobRunning);
        ToggleAdvancedPanelCommand = new RelayCommand(DoToggleAdvanced, () => CanOpenAdvancedPanel);
        DismissSummaryCommand = new RelayCommand(ResetForNextJob);
        OpenHelpCommand = new RelayCommand<string>(key =>
        {
            var shell = System.Windows.Application.Current.Windows
                .OfType<NepDateWidget.Views.ExpandedShellWindow>()
                .FirstOrDefault(w => w.IsVisible)
                ?? (System.Windows.Window)System.Windows.Application.Current.MainWindow!;
            NepDateWidget.Views.HelpPopup.ShowFor(key!, _loc, shell);
        });

        _orchestrator.Progress += OnOrchestratorProgress;
    }

    // ── Command implementations ──────────────────────────────────────────────

    private void DoLoadFiles()
    {
        var dlg = new Microsoft.Win32.OpenFileDialog
        {
            Multiselect = true,
            Filter = "Supported files|*.jpg;*.jpeg;*.png;*.webp;*.gif;*.tif;*.tiff;*.bmp;*.heic;*.heif;*.avif;*.pdf|All files (*.*)|*.*",
        };
        if (dlg.ShowDialog() != true)
        {
            return;
        }

        AddFiles(dlg.FileNames);
    }

    public void AddFiles(IReadOnlyList<string> paths)
    {
        CancelPendingAutoReset();
        if (paths.Count == 0)
        {
            return;
        }

        if (_isJobComplete)
        {
            Files.Clear();
            IsJobComplete = false;
            JobSummary = string.Empty;
            SummaryFilesSegment = string.Empty;
            SummaryNewSizeSegment = string.Empty;
            SummaryOrigSizeSegment = string.Empty;
            SummarySavedSegment = string.Empty;
        }

        // Validate same-type constraint across new + existing paths.
        var allPaths = Files.Select(f => f.FilePath).Concat(paths).ToList();
        var error = _fileTypeService.ValidateSameType(allPaths);
        if (error is not null)
        {
            MixedTypeWarning = _loc.Get("compress.mixed_type_warning");
            // Still add so the user can see what was loaded - warn but allow removal.
        }
        else
        {
            MixedTypeWarning = string.Empty;
        }

        foreach (var path in paths)
        {
            var ext = Path.GetExtension(path);
            var mime = _fileTypeService.GetMimeType(ext);
            if (mime is null)
            {
                continue;  // silently skip unsupported extensions
            }

            // If the same path is already in the list, replace it with a fresh entry.
            var existing = Files.FirstOrDefault(f => string.Equals(f.FilePath, path, StringComparison.OrdinalIgnoreCase));
            if (existing is not null)
            {
                Files.Remove(existing);
            }

            Files.Add(new CompressionFileItemViewModel
            {
                FilePath = path,
                FileName = Path.GetFileName(path),
                FileSizeBytes = GetFileSizeBytes(path),
                Status = CompressionFileStatus.Pending,
            });
        }

        RefreshDetectedType();
        OnPropertyChanged(nameof(CanOpenAdvancedPanel));
        OnPropertyChanged(nameof(CanCompress));
        OnPropertyChanged(nameof(HasFiles));
        OnPropertyChanged(nameof(ShowFileList));
    }

    private void DoRemoveFile(string? filePath)
    {
        if (filePath is null)
        {
            return;
        }

        var item = Files.FirstOrDefault(f => f.FilePath == filePath);
        if (item is null)
        {
            return;
        }

        Files.Remove(item);

        RefreshDetectedType();
        if (Files.Count == 0)
        {
            MixedTypeWarning = string.Empty;
            IsAdvancedPanelOpen = false;
        }
        else
        {
            MixedTypeWarning = _fileTypeService.ValidateSameType(Files.Select(f => f.FilePath).ToList()) is not null
                ? _loc.Get("compress.mixed_type_warning")
                : string.Empty;
        }
        OnPropertyChanged(nameof(CanOpenAdvancedPanel));
        OnPropertyChanged(nameof(CanCompress));
        OnPropertyChanged(nameof(HasFiles));
        OnPropertyChanged(nameof(ShowFileList));
    }

    private async void DoCompress()
    {
        if (_isJobRunning) { DoCancel(); return; }
        if (!CanCompress)
        {
            return;
        }

        // Show output dialog.
        var outputDir = PickOutputDirectory(out var singleOutputPath);
        if (outputDir is null && singleOutputPath is null)
        {
            return;  // user dismissed
        }

        // Build jobs.
        var jobs = BuildJobs(outputDir, singleOutputPath);
        if (jobs.Count == 0)
        {
            return;
        }

        IsJobRunning = true;
        IsJobComplete = false;
        CompletedCount = 0;
        TotalCount = jobs.Count;

        foreach (var f in Files)
        {
            f.Status = CompressionFileStatus.Pending;
        }

        await _orchestrator.StartJobAsync(jobs);

        IsJobRunning = false;
        IsJobComplete = true;

        // Update per-file statuses.
        // (They were tracked via Progress events; jobs list has final paths.)
        ApplyJobResults(jobs);

        // Build summary from actual results (not planned job count) so users see
        // a truthful outcome when some files fail.
        int doneCount = Files.Count(f => f.IsDone);
        int failedCount = Files.Count - doneCount;
        long totalSaved = jobs.Sum(j => EstimateSaved(j.OutputPath, j.InputPath));
        long totalOut = jobs.Sum(j => GetFileSizeBytes(j.OutputPath));
        long totalIn = jobs.Sum(j => GetFileSizeBytes(j.InputPath));

        JobSummary = failedCount == 0
            ? (doneCount == 1
                ? string.Format(_loc.Get("compress.summary_done_one"), FormatBytes(totalSaved), FormatBytes(totalOut))
                : string.Format(_loc.Get("compress.summary_done_many"), doneCount, FormatBytes(totalSaved), FormatBytes(totalOut)))
            : string.Format(_loc.Get("compress.summary_partial"), doneCount, failedCount, FormatBytes(totalSaved), FormatBytes(totalOut));

        SummaryFilesSegment = failedCount == 0
            ? (doneCount == 1
                ? _loc.Get("compress.summary_seg_files_one")
                : string.Format(_loc.Get("compress.summary_seg_files_many"), doneCount))
            : string.Format(_loc.Get("compress.summary_seg_partial"), doneCount, failedCount);
        SummaryNewSizeSegment = string.Format(_loc.Get("compress.summary_seg_new_size"), FormatBytes(totalOut));
        SummaryOrigSizeSegment = string.Format(_loc.Get("compress.summary_seg_orig_size"), FormatBytes(totalIn));
        SummarySavedSegment = string.Format(_loc.Get("compress.summary_seg_saved"), FormatBytes(totalSaved));
        ScheduleAutoReset(failedCount > 0);
    }

    private void DoCancel()
    {
        _orchestrator.CancelJob();
    }

    private void DoToggleAdvanced()
    {
        if (!CanOpenAdvancedPanel)
        {
            return;
        }

        IsAdvancedPanelOpen = !_isAdvancedPanelOpen;
    }

    private void ResetForNextJob()
    {
        CancelPendingAutoReset();
        Files.Clear();
        _detectedMimeType = string.Empty;
        _detectedCategory = FileCategory.Unsupported;
        MixedTypeWarning = string.Empty;
        IsAdvancedPanelOpen = false;
        IsJobComplete = false;
        IsJobRunning = false;
        CompletedCount = 0;
        TotalCount = 0;
        JobSummary = string.Empty;
        SummaryFilesSegment = string.Empty;
        SummaryNewSizeSegment = string.Empty;
        SummaryOrigSizeSegment = string.Empty;
        SummarySavedSegment = string.Empty;
        ResizeWidthText = string.Empty;
        ResizeHeightText = string.Empty;

        OnPropertyChanged(nameof(CanOpenAdvancedPanel));
        OnPropertyChanged(nameof(CanCompress));
        OnPropertyChanged(nameof(HasFiles));
        OnPropertyChanged(nameof(ShowFileList));
        OnPropertyChanged(nameof(DetectedMimeType));
        OnPropertyChanged(nameof(DetectedCategory));
        RefreshMimeFlags();
    }

    private void ScheduleAutoReset(bool hasErrors)
    {
        CancelPendingAutoReset();
        _resetTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(hasErrors ? 8 : 3) };
        _resetTimer.Tick += (_, _) => { CancelPendingAutoReset(); ResetForNextJob(); };
        _resetTimer.Start();
    }

    private void CancelPendingAutoReset()
    {
        _resetTimer?.Stop();
        _resetTimer = null;
    }

    // ── Progress handler ─────────────────────────────────────────────────────

    private void OnOrchestratorProgress(object? sender, JobProgressState state)
    {
        // Fired from thread pool. Only process if this VM started the current job.
        // Both CompressVM and ResizeVM subscribe to the shared orchestrator; the guard
        // prevents the idle VM from updating its state during the other VM's job.
        if (!_isJobRunning)
        {
            return;
        }

        Application.Current?.Dispatcher.InvokeAsync(() =>
        {
            CompletedCount = state.CompletedCount;
            TotalCount = state.TotalCount;
        });
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private void RefreshDetectedType()
    {
        if (Files.Count == 0)
        {
            _detectedMimeType = string.Empty;
            _detectedCategory = FileCategory.Unsupported;
        }
        else
        {
            var ext = Path.GetExtension(Files[0].FilePath);
            _detectedMimeType = _fileTypeService.GetMimeType(ext) ?? string.Empty;
            _detectedCategory = _fileTypeService.GetCategory(_detectedMimeType);
        }

        OnPropertyChanged(nameof(DetectedMimeType));
        OnPropertyChanged(nameof(DetectedCategory));
        RefreshMimeFlags();

        // Reset advanced settings when type changes.
        AdvancedSettings = new AdvancedCompressionSettings();
        _stripMetadata = true;
        _convertToWebP = false;
        _losslessWebP = false;
        _optimizeGifFrames = true;
        _tiffCompression = "LZW";
        _linearizePdf = true;
        _resizeWidthText = string.Empty;
        _resizeHeightText = string.Empty;
        OnPropertyChanged(nameof(StripMetadata));
        OnPropertyChanged(nameof(ConvertToWebP));
        OnPropertyChanged(nameof(LosslessWebP));
        OnPropertyChanged(nameof(OptimizeGifFrames));
        OnPropertyChanged(nameof(TiffCompression));
        OnPropertyChanged(nameof(LinearizePdf));
        OnPropertyChanged(nameof(ResizeWidthText));
        OnPropertyChanged(nameof(ResizeHeightText));
    }

    private void RefreshMimeFlags()
    {
        OnPropertyChanged(nameof(IsMimeJpeg));
        OnPropertyChanged(nameof(IsMimePng));
        OnPropertyChanged(nameof(IsMimeWebP));
        OnPropertyChanged(nameof(IsMimeGif));
        OnPropertyChanged(nameof(IsMimeTiff));
        OnPropertyChanged(nameof(IsMimeBmp));
        OnPropertyChanged(nameof(IsMimeHeif));
        OnPropertyChanged(nameof(IsMimeAvif));
        OnPropertyChanged(nameof(IsMimePdf));
        OnPropertyChanged(nameof(IsImageType));
        OnPropertyChanged(nameof(IsMimeJpegOrPng));
        OnPropertyChanged(nameof(ShowLosslessWebP));
    }

    private string? PickOutputDirectory(out string? singleOutputPath)
    {
        singleOutputPath = null;

        if (Files.Count == 1)
        {
            var inputPath = Files[0].FilePath;
            var suggested = GetOutputFileName(inputPath, _detectedMimeType, isResize: false, convertToWebP: _convertToWebP);
            var saveDir = Path.GetDirectoryName(inputPath) ?? string.Empty;

            var dlg = new Microsoft.Win32.SaveFileDialog
            {
                FileName = Path.GetFileName(suggested),
                InitialDirectory = saveDir,
                Filter = BuildSaveFilter(suggested),
            };
            if (dlg.ShowDialog() != true)
            {
                return null;
            }

            singleOutputPath = dlg.FileName;
            return null; // signals single-file mode
        }
        else
        {
            // Multiple files - pick a folder.
            var dlg = new Microsoft.Win32.OpenFolderDialog
            {
                Title = "Select Output Folder",
            };
            if (dlg.ShowDialog() != true)
            {
                return null;
            }

            return dlg.FolderName;
        }
    }

    private IReadOnlyList<CompressionJob> BuildJobs(string? outputDir, string? singleOutputPath)
    {
        var settings = BuildSettings();
        var jobs = new List<CompressionJob>();

        for (int i = 0; i < Files.Count; i++)
        {
            var f = Files[i];
            var ext = Path.GetExtension(f.FilePath);
            var mime = _fileTypeService.GetMimeType(ext) ?? string.Empty;
            var cat = _fileTypeService.GetCategory(mime);

            string outPath;
            if (singleOutputPath is not null)
            {
                outPath = singleOutputPath;
            }
            else
            {
                var outName = GetOutputFileName(f.FilePath, mime, isResize: false, convertToWebP: _convertToWebP);
                outPath = Path.Combine(outputDir!, Path.GetFileName(outName));
            }

            jobs.Add(new CompressionJob
            {
                InputPath = f.FilePath,
                OutputPath = outPath,
                Settings = settings,
                Category = cat,
                MimeType = mime,
            });
        }

        return jobs;
    }

    private CompressionSettings BuildSettings()
    {
        return new CompressionSettings
        {
            CompressionLevel = _compressionLevel,
            ResizeWidth = _detectedCategory == FileCategory.Image ? ParsePositiveUInt(_resizeWidthText) : null,
            ResizeHeight = _detectedCategory == FileCategory.Image ? ParsePositiveUInt(_resizeHeightText) : null,
            Advanced = new AdvancedCompressionSettings
            {
                StripMetadata = _stripMetadata,
                ConvertToWebP = _convertToWebP,
                LosslessWebP = _losslessWebP,
                OptimizeGifFrames = _optimizeGifFrames,
                TiffCompression = _tiffCompression,
                LinearizePdf = _linearizePdf,
            },
        };
    }

    private void ApplyJobResults(IReadOnlyList<CompressionJob> jobs)
    {
        for (int i = 0; i < jobs.Count && i < Files.Count; i++)
        {
            var job = jobs[i];
            var item = Files.FirstOrDefault(f => f.FilePath == job.InputPath);
            if (item is null)
            {
                continue;
            }

            item.Status = File.Exists(job.OutputPath) ? CompressionFileStatus.Done : CompressionFileStatus.Error;
            item.OutputSizeBytes = item.Status == CompressionFileStatus.Done ? GetFileSizeBytes(job.OutputPath) : 0;
            item.NotifyStatus();
        }
    }

    // ── Static helpers ───────────────────────────────────────────────────────

    internal static string GetOutputFileName(string inputPath, string mimeType, bool isResize, bool convertToWebP = false)
    {
        var dir = Path.GetDirectoryName(inputPath) ?? string.Empty;
        var stem = Path.GetFileNameWithoutExtension(inputPath);
        var suffix = isResize ? "_Resized" : "_Compressed";

        string ext = mimeType.ToLowerInvariant() switch
        {
            "image/bmp" => ".jpg",
            "image/heif" => ".jpg",
            "image/jpeg" when convertToWebP => ".webp",
            "image/png" when convertToWebP => ".webp",
            _ => Path.GetExtension(inputPath).ToLowerInvariant(),
        };

        return Path.Combine(dir, stem + suffix + ext);
    }

    private static string BuildSaveFilter(string outputFileName)
    {
        var ext = Path.GetExtension(outputFileName).ToUpperInvariant().TrimStart('.');
        return $"{ext} files (*.{ext.ToLowerInvariant()})|*.{ext.ToLowerInvariant()}|All files (*.*)|*.*";
    }

    private static long EstimateSaved(string outputPath, string inputPath)
    {
        long original = GetFileSizeBytes(inputPath);
        long compressed = GetFileSizeBytes(outputPath);
        return Math.Max(0L, original - compressed);
    }

    private static uint? ParsePositiveUInt(string text)
    {
        if (uint.TryParse(text, out var v) && v > 0)
        {
            return v;
        }

        return null;
    }

    // ── Language support ──────────────────────────────────────────────────────

    public void OnLanguageChanged()
    {
        OnPropertyChanged(nameof(CompressButtonLabel));
        OnPropertyChanged(nameof(ProgressLabel));
        OnPropertyChanged(nameof(DropLabel));
        OnPropertyChanged(nameof(BrowseLabel));
        OnPropertyChanged(nameof(CancelLabel));
        OnPropertyChanged(nameof(ProgressTitleLabel));
        OnPropertyChanged(nameof(LevelSectionLabel));
        OnPropertyChanged(nameof(LevelSmallestLabel));
        OnPropertyChanged(nameof(LevelLowLabel));
        OnPropertyChanged(nameof(LevelBalancedLabel));
        OnPropertyChanged(nameof(LevelHighLabel));
        OnPropertyChanged(nameof(LevelBestLabel));
        OnPropertyChanged(nameof(AdvancedBtnLabel));
        OnPropertyChanged(nameof(AdvImageLabel));
        OnPropertyChanged(nameof(AdvOptionalResizeLabel));
        OnPropertyChanged(nameof(AdvStripMetaLabel));
        OnPropertyChanged(nameof(AdvToWebPLabel));
        OnPropertyChanged(nameof(AdvLosslessWebPLabel));
        OnPropertyChanged(nameof(AdvGifLabel));
        OnPropertyChanged(nameof(AdvOptimizeGifLabel));
        OnPropertyChanged(nameof(AdvTiffLabel));
        OnPropertyChanged(nameof(AdvPdfLabel));
        OnPropertyChanged(nameof(AdvLinearizeLabel));
        OnPropertyChanged(nameof(AdvResizeWidthHint));
        OnPropertyChanged(nameof(AdvResizeHeightHint));
        OnPropertyChanged(nameof(AdvAspectHintLabel));
        OnPropertyChanged(nameof(RemoveFileTooltipLabel));
        OnPropertyChanged(nameof(AdvResizeWidthTooltip));
        OnPropertyChanged(nameof(AdvResizeHeightTooltip));
    }
}

/// <summary>
/// Per-file row item in the Compression file list. Exposes change notification for Status.
/// </summary>
public sealed class CompressionFileItemViewModel : ViewModelBase
{
    public required string FilePath { get; init; }
    public required string FileName { get; init; }
    public long FileSizeBytes { get; init; }
    public long OutputSizeBytes { get; set; }

    private CompressionFileStatus _status = CompressionFileStatus.Pending;
    public CompressionFileStatus Status
    {
        get => _status;
        set
        {
            if (SetProperty(ref _status, value))
            {
                OnPropertyChanged(nameof(IsPending));
                OnPropertyChanged(nameof(IsRunning));
                OnPropertyChanged(nameof(IsDone));
                OnPropertyChanged(nameof(IsError));
                OnPropertyChanged(nameof(ErrorMessage));
            }
        }
    }

    public bool IsPending => _status == CompressionFileStatus.Pending;
    public bool IsRunning => _status == CompressionFileStatus.Running;
    public bool IsDone => _status == CompressionFileStatus.Done;
    public bool IsError => _status == CompressionFileStatus.Error;

    public string StatusText => _status switch
    {
        CompressionFileStatus.Done => "Done",
        CompressionFileStatus.Error => "Error",
        CompressionFileStatus.Running => "Running",
        _ => "Pending",
    };

    public string StatusGlyph => _status switch
    {
        CompressionFileStatus.Done => "✓",
        CompressionFileStatus.Error => "✗",
        CompressionFileStatus.Running => "…",
        _ => "·",
    };

    public string SavingsLabel
    {
        get
        {
            if (_status != CompressionFileStatus.Done || FileSizeBytes <= 0 || OutputSizeBytes <= 0)
            {
                return string.Empty;
            }

            var pct = (1.0 - (double)OutputSizeBytes / FileSizeBytes) * 100;
            return pct > 0.5 ? $"-{pct:F0}%" : pct < -0.5 ? $"+{-pct:F0}%" : "≈0%";
        }
    }

    public string? ErrorMessage { get; set; }

    public string FileSizeLabel => FormatBytes(FileSizeBytes);
    public string OutputSizeLabel => OutputSizeBytes > 0 ? FormatBytes(OutputSizeBytes) : string.Empty;

    private string _dimensions = string.Empty;
    public string Dimensions
    {
        get => _dimensions;
        set
        {
            if (SetProperty(ref _dimensions, value))
                OnPropertyChanged(nameof(HasDimensions));
        }
    }
    public bool HasDimensions => !string.IsNullOrEmpty(_dimensions);

    public void NotifyStatus()
    {
        OnPropertyChanged(nameof(Status));
        OnPropertyChanged(nameof(IsPending));
        OnPropertyChanged(nameof(IsRunning));
        OnPropertyChanged(nameof(IsDone));
        OnPropertyChanged(nameof(IsError));
        OnPropertyChanged(nameof(StatusText));
        OnPropertyChanged(nameof(StatusGlyph));
        OnPropertyChanged(nameof(SavingsLabel));
        OnPropertyChanged(nameof(OutputSizeLabel));
    }
}
