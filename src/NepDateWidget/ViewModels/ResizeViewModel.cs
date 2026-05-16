using NepDateWidget.Helpers;
using NepDateWidget.Models;
using NepDateWidget.Services;
using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Input;

namespace NepDateWidget.ViewModels;

/// <summary>
/// Manages the Resize sub-view state.
/// Singleton - survives shell expand/collapse cycles.
/// </summary>
public sealed class ResizeViewModel : ViewModelBase
{
    private readonly IFileTypeService         _fileTypeService;
    private readonly IJobOrchestrationService _orchestrator;
    private readonly ILocalizationService     _loc;

    // ── File list ────────────────────────────────────────────────────────────

    public ObservableCollection<CompressionFileItemViewModel> Files { get; } = new();

    private string _detectedMimeType = string.Empty;
    private FileCategory _detectedCategory = FileCategory.Unsupported;

    public bool IsPdfLoaded => _detectedCategory == FileCategory.Pdf;
    public bool IsImageLoaded => _detectedCategory == FileCategory.Image && Files.Count > 0;
    public bool HasFiles => Files.Count > 0;

    // ── Resize dimensions ────────────────────────────────────────────────────

    private string _widthText = string.Empty;
    public string WidthText
    {
        get => _widthText;
        set
        {
            if (SetProperty(ref _widthText, value))
            {
                OnPropertyChanged(nameof(ShowStretchWarning));
                OnPropertyChanged(nameof(CanResize));
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
                OnPropertyChanged(nameof(ShowStretchWarning));
                OnPropertyChanged(nameof(CanResize));
            }
        }
    }

    // Stretch warning: show when both dimensions are provided and ratio differs by >1% from the original.
    public bool ShowStretchWarning
    {
        get
        {
            if (!uint.TryParse(_widthText, out uint w) || w == 0) return false;
            if (!uint.TryParse(_heightText, out uint h) || h == 0) return false;
            if (Files.Count == 0) return false;

            try
            {
                var info = GetImageDimensions(Files[0].FilePath);
                if (info.width == 0 || info.height == 0) return false;

                double origRatio = (double)info.width / info.height;
                double newRatio  = (double)w / h;
                return Math.Abs(origRatio - newRatio) / origRatio > 0.01;
            }
            catch { return false; }
        }
    }

    // ── Also compress toggle ─────────────────────────────────────────────────

    private bool _alsoCompress;
    public bool AlsoCompress
    {
        get => _alsoCompress;
        set
        {
            if (SetProperty(ref _alsoCompress, value))
                OnPropertyChanged(nameof(ShowQualitySlider));
        }
    }

    public bool ShowQualitySlider => _alsoCompress;

    private int _qualityLevel = 1;
    public int QualityLevel
    {
        get => _qualityLevel;
        set => SetProperty(ref _qualityLevel, Math.Clamp(value, 0, 4));
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
                OnPropertyChanged(nameof(CanResize));
                OnPropertyChanged(nameof(ResizeButtonLabel));
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
                OnPropertyChanged(nameof(ShowSummary));
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

    public string ProgressLabel    => $"{_completedCount} {_loc.Get("compress.progress_of")} {_totalCount}";
    public bool   ShowSummary      => _isJobComplete;

    private string _jobSummary = string.Empty;
    public string JobSummary
    {
        get => _jobSummary;
        private set => SetProperty(ref _jobSummary, value);
    }

    // ── Mixed-type warning ───────────────────────────────────────────────────

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

    public string ResizeButtonLabel => _isJobRunning ? _loc.Get("compress.cancel_btn") : _loc.Get("compress.resize_btn");

    // ── Localized labels ─────────────────────────────────────────────────────

    public string DropLabel              => _loc.Get("compress.drop_images");
    public string BrowseLabel            => _loc.Get("compress.browse");
    public string CancelLabel            => _loc.Get("compress.cancel_btn");
    public string ProgressTitleLabel     => _loc.Get("compress.resizing");
    public string DimensionsSectionLabel => _loc.Get("compress.dimensions_section");
    public string WidthLabel             => _loc.Get("compress.width_label");
    public string HeightLabel            => _loc.Get("compress.height_label");
    public string AspectRatioHintLabel   => _loc.Get("compress.aspect_ratio_hint");
    public string StretchWarningLabel    => _loc.Get("compress.stretch_warning");
    public string PdfNotSupportedLabel   => _loc.Get("compress.pdf_not_supported");
    public string AlsoCompressLabel      => _loc.Get("compress.also_compress");
    public string LevelSectionLabel      => _loc.Get("compress.level_section");
    public string LevelSmallestLabel     => _loc.Get("compress.level_smallest");
    public string LevelLowLabel          => _loc.Get("compress.level_low");
    public string LevelBalancedLabel     => _loc.Get("compress.level_balanced");
    public string LevelHighLabel         => _loc.Get("compress.level_high");
    public string LevelBestLabel         => _loc.Get("compress.level_best");
    public string RemoveFileTooltipLabel  => _loc.Get("compress.remove_file");

    public bool CanResize
    {
        get
        {
            if (Files.Count == 0 || _isJobRunning || _isJobComplete || HasMixedTypeWarning || IsPdfLoaded) return false;
            bool hasW = uint.TryParse(_widthText, out uint w)  && w > 0;
            bool hasH = uint.TryParse(_heightText, out uint h) && h > 0;
            return hasW || hasH;
        }
    }

    // ── Commands ─────────────────────────────────────────────────────────────

    public ICommand LoadFilesCommand   { get; }
    public ICommand RemoveFileCommand  { get; }
    public ICommand ResizeCommand      { get; }
    public ICommand CancelCommand      { get; }
    public ICommand OpenHelpCommand    { get; }

    private CancellationTokenSource? _autoResetCts;

    // ── Construction ─────────────────────────────────────────────────────────

    public ResizeViewModel(IFileTypeService fileTypeService, IJobOrchestrationService orchestrator, ILocalizationService loc)
    {
        _fileTypeService = fileTypeService ?? throw new ArgumentNullException(nameof(fileTypeService));
        _orchestrator    = orchestrator    ?? throw new ArgumentNullException(nameof(orchestrator));
        _loc             = loc             ?? throw new ArgumentNullException(nameof(loc));

        LoadFilesCommand   = new RelayCommand(DoLoadFiles);
        RemoveFileCommand  = new RelayCommand<string>(DoRemoveFile);
        ResizeCommand      = new RelayCommand(DoResize, () => CanResize);
        CancelCommand      = new RelayCommand(DoCancel, () => _isJobRunning);
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
            Filter = "Image files|*.jpg;*.jpeg;*.png;*.webp;*.gif;*.tif;*.tiff;*.bmp;*.heic;*.heif;*.avif|All files (*.*)|*.*",
        };
        if (dlg.ShowDialog() != true) return;
        AddFiles(dlg.FileNames);
    }

    public void AddFiles(IReadOnlyList<string> paths)
    {
        if (paths.Count == 0) return;

        CancelPendingAutoReset();

        var allPaths = Files.Select(f => f.FilePath).Concat(paths).ToList();
        var error    = _fileTypeService.ValidateSameType(allPaths);
        MixedTypeWarning = error is not null ? _loc.Get("compress.mixed_type_warning") : string.Empty;

        foreach (var path in paths)
        {
            var ext  = Path.GetExtension(path);
            var mime = _fileTypeService.GetMimeType(ext);
            if (mime is null) continue;

            // If the same path is already in the list, replace it with a fresh entry.
            var existing = Files.FirstOrDefault(f => string.Equals(f.FilePath, path, StringComparison.OrdinalIgnoreCase));
            if (existing is not null) Files.Remove(existing);

            Files.Add(new CompressionFileItemViewModel
            {
                FilePath      = path,
                FileName      = Path.GetFileName(path),
                FileSizeBytes = GetFileSizeBytes(path),
                Status        = CompressionFileStatus.Pending,
            });
        }

        RefreshDetectedType();
        OnPropertyChanged(nameof(CanResize));
        OnPropertyChanged(nameof(ShowStretchWarning));
    }

    private void DoRemoveFile(string? filePath)
    {
        if (filePath is null) return;
        var item = Files.FirstOrDefault(f => f.FilePath == filePath);
        if (item is null) return;
        Files.Remove(item);
        RefreshDetectedType();
        MixedTypeWarning = Files.Count > 0
            ? (_fileTypeService.ValidateSameType(Files.Select(f => f.FilePath).ToList()) is not null
                ? _loc.Get("compress.mixed_type_warning")
                : string.Empty)
            : string.Empty;
        OnPropertyChanged(nameof(CanResize));
        OnPropertyChanged(nameof(ShowStretchWarning));
        OnPropertyChanged(nameof(HasFiles));
    }

    private async void DoResize()
    {
        if (_isJobRunning) { DoCancel(); return; }
        if (!CanResize) return;

        CancelPendingAutoReset();

        var outputDir = PickOutputDirectory(out var singleOutputPath);
        if (outputDir is null && singleOutputPath is null) return;

        uint? w = uint.TryParse(_widthText, out uint wv) && wv > 0 ? wv : null;
        uint? h = uint.TryParse(_heightText, out uint hv) && hv > 0 ? hv : null;

        var jobs = BuildJobs(outputDir, singleOutputPath, w, h);
        if (jobs.Count == 0) return;

        IsJobRunning  = true;
        IsJobComplete = false;
        CompletedCount = 0;
        TotalCount     = jobs.Count;

        foreach (var f in Files) f.Status = CompressionFileStatus.Pending;

        await _orchestrator.StartJobAsync(jobs);

        IsJobRunning  = false;
        IsJobComplete = true;

        ApplyJobResults(jobs);

        int doneCount = Files.Count(f => f.IsDone);
        int failedCount = Files.Count - doneCount;
        long totalSaved = jobs.Sum(j => EstimateSaved(j.OutputPath, j.InputPath));
        long totalOut   = jobs.Sum(j => GetFileSizeBytes(j.OutputPath));
        JobSummary = failedCount == 0
            ? (doneCount == 1
                ? string.Format(_loc.Get("compress.summary_done_one"), FormatBytes(totalSaved), FormatBytes(totalOut))
                : string.Format(_loc.Get("compress.summary_done_many"), doneCount, FormatBytes(totalSaved), FormatBytes(totalOut)))
            : string.Format(_loc.Get("compress.summary_partial"), doneCount, failedCount, FormatBytes(totalSaved), FormatBytes(totalOut));

        // Keep failures visible longer so users can inspect status, then reset.
        ScheduleAutoReset(failedCount == 0 ? TimeSpan.FromSeconds(3) : TimeSpan.FromSeconds(8));
    }

    private void DoCancel()
    {
        _orchestrator.CancelJob();
    }

    private void ResetForNextJob()
    {
        Files.Clear();
        _detectedMimeType = string.Empty;
        _detectedCategory = FileCategory.Unsupported;
        MixedTypeWarning  = string.Empty;
        IsJobComplete     = false;
        IsJobRunning      = false;
        CompletedCount    = 0;
        TotalCount        = 0;
        JobSummary        = string.Empty;
        WidthText         = string.Empty;
        HeightText        = string.Empty;
        AlsoCompress      = false;
        OnPropertyChanged(nameof(CanResize));
        OnPropertyChanged(nameof(IsPdfLoaded));
        OnPropertyChanged(nameof(IsImageLoaded));
        OnPropertyChanged(nameof(HasFiles));
    }

    private void CancelPendingAutoReset()
    {
        if (_autoResetCts is null) return;
        try { _autoResetCts.Cancel(); } catch { }
        _autoResetCts.Dispose();
        _autoResetCts = null;
    }

    private void ScheduleAutoReset(TimeSpan delay)
    {
        CancelPendingAutoReset();
        _autoResetCts = new CancellationTokenSource();
        var token = _autoResetCts.Token;

        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(delay, token).ConfigureAwait(false);
                if (token.IsCancellationRequested) return;

                await Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    if (!token.IsCancellationRequested)
                        ResetForNextJob();
                });
            }
            catch (TaskCanceledException) { }
        }, token);
    }

    // ── Progress handler ─────────────────────────────────────────────────────

    private void OnOrchestratorProgress(object? sender, JobProgressState state)
    {
        // Only process if this VM started the current job.
        if (!_isJobRunning) return;
        Application.Current?.Dispatcher.InvokeAsync(() =>
        {
            CompletedCount = state.CompletedCount;
            TotalCount     = state.TotalCount;
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
        OnPropertyChanged(nameof(IsPdfLoaded));
        OnPropertyChanged(nameof(IsImageLoaded));
        OnPropertyChanged(nameof(HasFiles));
    }

    private string? PickOutputDirectory(out string? singleOutputPath)
    {
        singleOutputPath = null;

        if (Files.Count == 1)
        {
            var inputPath = Files[0].FilePath;
            var suggested = CompressionViewModel.GetOutputFileName(inputPath, _detectedMimeType, isResize: true);
            var saveDir   = Path.GetDirectoryName(inputPath) ?? string.Empty;

            var dlg = new Microsoft.Win32.SaveFileDialog
            {
                FileName         = Path.GetFileName(suggested),
                InitialDirectory = saveDir,
                Filter           = "Image files|*.jpg;*.jpeg;*.png;*.webp;*.gif;*.tif;*.tiff;*.bmp;*.avif|All files|*.*",
            };
            if (dlg.ShowDialog() != true) return null;
            singleOutputPath = dlg.FileName;
            return null;
        }
        else
        {
            var dlg = new Microsoft.Win32.OpenFolderDialog { Title = "Select Output Folder" };
            if (dlg.ShowDialog() != true) return null;
            return dlg.FolderName;
        }
    }

    private IReadOnlyList<CompressionJob> BuildJobs(string? outputDir, string? singleOutputPath, uint? w, uint? h)
    {
        var settings = new CompressionSettings
        {
            CompressionLevel = _alsoCompress ? _qualityLevel : 4,
            ResizeWidth      = w,
            ResizeHeight     = h,
            Advanced         = new AdvancedCompressionSettings
            {
                StripMetadata = true,
            },
        };

        var jobs = new List<CompressionJob>();
        for (int i = 0; i < Files.Count; i++)
        {
            var f    = Files[i];
            var ext  = Path.GetExtension(f.FilePath);
            var mime = _fileTypeService.GetMimeType(ext) ?? string.Empty;
            var cat  = _fileTypeService.GetCategory(mime);

            string outPath;
            if (singleOutputPath is not null)
                outPath = singleOutputPath;
            else
            {
                var outName = CompressionViewModel.GetOutputFileName(f.FilePath, mime, isResize: true);
                outPath     = Path.Combine(outputDir!, Path.GetFileName(outName));
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

    private void ApplyJobResults(IReadOnlyList<CompressionJob> jobs)
    {
        for (int i = 0; i < jobs.Count && i < Files.Count; i++)
        {
            var job  = jobs[i];
            var item = Files.FirstOrDefault(f => f.FilePath == job.InputPath);
            if (item is null) continue;
            item.Status = File.Exists(job.OutputPath) ? CompressionFileStatus.Done : CompressionFileStatus.Error;
            item.NotifyStatus();
        }
    }

    private static (int width, int height) GetImageDimensions(string path)
    {
        using var img = new ImageMagick.MagickImage();
        img.Ping(path);  // reads header only, no pixel decode
        return ((int)img.Width, (int)img.Height);
    }

    private static long GetFileSizeBytes(string path)
    {
        try { return new FileInfo(path).Length; } catch { return 0; }
    }

    private static long EstimateSaved(string outputPath, string inputPath)
    {
        long original = GetFileSizeBytes(inputPath);
        long output   = GetFileSizeBytes(outputPath);
        return Math.Max(0L, original - output);
    }

    private static string FormatBytes(long bytes)
    {
        if (bytes <= 0) return "0 B";
        if (bytes < 1024) return $"{bytes} B";
        if (bytes < 1024 * 1024) return $"{bytes / 1024.0:F1} KB";
        return $"{bytes / (1024.0 * 1024):F1} MB";
    }

    // ── Language support ──────────────────────────────────────────────────────

    public void OnLanguageChanged()
    {
        OnPropertyChanged(nameof(ResizeButtonLabel));
        OnPropertyChanged(nameof(ProgressLabel));
        OnPropertyChanged(nameof(DropLabel));
        OnPropertyChanged(nameof(BrowseLabel));
        OnPropertyChanged(nameof(CancelLabel));
        OnPropertyChanged(nameof(ProgressTitleLabel));
        OnPropertyChanged(nameof(DimensionsSectionLabel));
        OnPropertyChanged(nameof(WidthLabel));
        OnPropertyChanged(nameof(HeightLabel));
        OnPropertyChanged(nameof(AspectRatioHintLabel));
        OnPropertyChanged(nameof(StretchWarningLabel));
        OnPropertyChanged(nameof(PdfNotSupportedLabel));
        OnPropertyChanged(nameof(AlsoCompressLabel));
        OnPropertyChanged(nameof(LevelSectionLabel));
        OnPropertyChanged(nameof(LevelSmallestLabel));
        OnPropertyChanged(nameof(LevelLowLabel));
        OnPropertyChanged(nameof(LevelBalancedLabel));
        OnPropertyChanged(nameof(LevelHighLabel));
        OnPropertyChanged(nameof(LevelBestLabel));
        OnPropertyChanged(nameof(RemoveFileTooltipLabel));
    }
}
