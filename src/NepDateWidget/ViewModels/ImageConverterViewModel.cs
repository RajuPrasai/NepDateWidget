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
/// Manages the Image Converter sub-view. Singleton — survives shell expand/collapse cycles.
/// </summary>
public sealed class ImageConverterViewModel : ViewModelBase
{
    private readonly IImageConversionService _conversionService;
    private readonly ILocalizationService _loc;

    private CancellationTokenSource? _cts;
    private DispatcherTimer? _resetTimer;

    // ── File list ────────────────────────────────────────────────────────────

    public ObservableCollection<CompressionFileItemViewModel> Files { get; } = new();

    // ── Format selection ─────────────────────────────────────────────────────

    private string _selectedFormatExt = "jpg";
    public string SelectedFormatExt
    {
        get => _selectedFormatExt;
        private set
        {
            if (SetProperty(ref _selectedFormatExt, value))
            {
                OnPropertyChanged(nameof(ShowQuality));
                OnPropertyChanged(nameof(ShowQualitySlider));
                RefreshFormatFlags();
            }
        }
    }

    public bool IsFormatJpeg => _selectedFormatExt == "jpg";
    public bool IsFormatPng  => _selectedFormatExt == "png";
    public bool IsFormatWebp => _selectedFormatExt == "webp";
    public bool IsFormatAvif => _selectedFormatExt == "avif";
    public bool IsFormatGif  => _selectedFormatExt == "gif";
    public bool IsFormatBmp  => _selectedFormatExt == "bmp";
    public bool IsFormatTiff => _selectedFormatExt == "tif";
    public bool IsFormatIco  => _selectedFormatExt == "ico";
    public bool IsFormatTga  => _selectedFormatExt == "tga";

    // Quality section is visible only for lossy formats.
    public bool ShowQuality => _selectedFormatExt is "jpg" or "webp" or "avif";
    // Quality slider is visible only when quality section is shown AND compression is enabled.
    public bool ShowQualitySlider => ShowQuality && _applyCompression;

    // ── Compression toggle ───────────────────────────────────────────────────

    private bool _applyCompression = true;
    public bool ApplyCompression
    {
        get => _applyCompression;
        set
        {
            if (SetProperty(ref _applyCompression, value))
                OnPropertyChanged(nameof(ShowQualitySlider));
        }
    }

    // ── Quality level (0 = smallest / lowest quality, 4 = best quality) ─────

    private int _qualityLevel = 2;
    public int QualityLevel
    {
        get => _qualityLevel;
        set => SetProperty(ref _qualityLevel, Math.Clamp(value, 0, 4));
    }

    // ── Strip metadata ───────────────────────────────────────────────────────

    private bool _stripMetadata = true;
    public bool StripMetadata
    {
        get => _stripMetadata;
        set => SetProperty(ref _stripMetadata, value);
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
                OnPropertyChanged(nameof(CanConvert));
                OnPropertyChanged(nameof(IsConvertButtonEnabled));
                OnPropertyChanged(nameof(ConvertButtonLabel));
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
                OnPropertyChanged(nameof(CanConvert));
                OnPropertyChanged(nameof(IsConvertButtonEnabled));
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

    public string ProgressLabel => $"{_completedCount} {_loc.Get("imgconv.progress_of")} {_totalCount}";

    private string _summaryFilesSegment = string.Empty;
    public string SummaryFilesSegment
    {
        get => _summaryFilesSegment;
        private set => SetProperty(ref _summaryFilesSegment, value);
    }

    private string _summarySizeSegment = string.Empty;
    public string SummarySizeSegment
    {
        get => _summarySizeSegment;
        private set => SetProperty(ref _summarySizeSegment, value);
    }

    public bool ShowSummary          => _isJobComplete;
    public bool HasFiles              => Files.Count > 0;
    public bool ShowFileList          => HasFiles && !ShowSummary;
    public bool CanConvert            => Files.Count > 0 && !_isJobRunning && !_isJobComplete;
    // Enables the button as Convert when idle-with-files, and as Cancel when running.
    public bool IsConvertButtonEnabled => CanConvert || _isJobRunning;

    public string ConvertButtonLabel => _isJobRunning
        ? _loc.Get("imgconv.cancel_btn")
        : _loc.Get("imgconv.convert_btn");

    // ── Localized labels ─────────────────────────────────────────────────────

    public string DropLabel           => _loc.Get("imgconv.drop");
    public string BrowseLabel         => _loc.Get("imgconv.browse");
    public string FormatSectionLabel  => _loc.Get("imgconv.format_section");
    public string QualitySectionLabel => _loc.Get("imgconv.quality_section");
    public string LevelSmallestLabel  => _loc.Get("imgconv.level_smallest");
    public string LevelLowLabel       => _loc.Get("imgconv.level_low");
    public string LevelBalancedLabel  => _loc.Get("imgconv.level_balanced");
    public string LevelHighLabel      => _loc.Get("imgconv.level_high");
    public string LevelBestLabel      => _loc.Get("imgconv.level_best");
    public string StripMetaLabel        => _loc.Get("imgconv.strip_meta");
    public string CompressToggleLabel   => _loc.Get("imgconv.compress_toggle");
    public string RemoveFileLabel       => _loc.Get("imgconv.remove_file");
    public string ProgressTitleLabel    => _loc.Get("imgconv.converting");

    // ── Commands ─────────────────────────────────────────────────────────────

    public ICommand LoadFilesCommand    { get; }
    public ICommand RemoveFileCommand   { get; }
    public ICommand SelectFormatCommand { get; }
    public ICommand ConvertCommand      { get; }
    public ICommand DismissSummaryCommand { get; }
    public ICommand OpenHelpCommand     { get; }

    // ── Construction ─────────────────────────────────────────────────────────

    public ImageConverterViewModel(IImageConversionService conversionService, ILocalizationService loc)
    {
        _conversionService = conversionService ?? throw new ArgumentNullException(nameof(conversionService));
        _loc = loc ?? throw new ArgumentNullException(nameof(loc));

        LoadFilesCommand    = new RelayCommand(DoLoadFiles);
        RemoveFileCommand   = new RelayCommand<string>(DoRemoveFile);
        SelectFormatCommand = new RelayCommand<string>(DoSelectFormat);
        ConvertCommand      = new RelayCommand(DoConvert, () => CanConvert || _isJobRunning);
        DismissSummaryCommand = new RelayCommand(ResetForNextJob);
        OpenHelpCommand     = new RelayCommand<string>(key =>
        {
            var shell = Application.Current.Windows
                .OfType<NepDateWidget.Views.ExpandedShellWindow>()
                .FirstOrDefault(w => w.IsVisible)
                ?? (Window)Application.Current.MainWindow!;
            NepDateWidget.Views.HelpPopup.ShowFor(key!, _loc, shell);
        });
    }

    // ── Public entry point for drag-drop from view ───────────────────────────

    // Supported extensions for both folder expansion and drop filtering.
    private static readonly HashSet<string> _supportedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".png", ".webp", ".avif", ".gif", ".bmp",
        ".tif", ".tiff", ".heic", ".heif", ".psd", ".psb", ".ico",
        ".tga", ".dds", ".exr", ".arw", ".cr2", ".cr3", ".dng",
        ".nef", ".orf", ".raf", ".rw2", ".erf", ".pef", ".x3f"
    };

    private static IEnumerable<string> ExpandPaths(IReadOnlyList<string> paths)
    {
        foreach (var path in paths)
        {
            if (Directory.Exists(path))
            {
                var opts = new EnumerationOptions
                {
                    RecurseSubdirectories = true,
                    IgnoreInaccessible    = true,
                };
                foreach (var f in Directory.EnumerateFiles(path, "*", opts))
                    if (_supportedExtensions.Contains(Path.GetExtension(f)))
                        yield return f;
            }
            else if (_supportedExtensions.Contains(Path.GetExtension(path)))
            {
                yield return path;
            }
        }
    }

    public void AddFiles(IReadOnlyList<string> paths)
    {
        if (paths.Count == 0) return;

        CancelPendingReset();

        if (_isJobComplete)
            InternalReset();

        foreach (var path in ExpandPaths(paths))
        {
            // If the same path is already in the list, replace it.
            var existing = Files.FirstOrDefault(f =>
                string.Equals(f.FilePath, path, StringComparison.OrdinalIgnoreCase));
            if (existing is not null)
                Files.Remove(existing);

            var info = new FileInfo(path);
            Files.Add(new CompressionFileItemViewModel
            {
                FilePath      = path,
                FileName      = Path.GetFileName(path),
                FileSizeBytes = info.Exists ? info.Length : 0,
                Status        = CompressionFileStatus.Pending,
            });
        }

        RefreshFileState();
    }

    // ── Command implementations ──────────────────────────────────────────────

    private void DoLoadFiles()
    {
        var dlg = new Microsoft.Win32.OpenFileDialog
        {
            Multiselect = true,
            Filter = "Image files|*.jpg;*.jpeg;*.png;*.webp;*.avif;*.gif;*.bmp;*.tif;*.tiff;*.heic;*.heif;*.psd;*.psb;*.ico;*.tga;*.dds;*.exr;*.svg;*.arw;*.cr2;*.cr3;*.dng;*.nef;*.orf;*.raf;*.rw2;*.erf;*.pef;*.x3f|All files (*.*)|*.*",
        };
        if (dlg.ShowDialog() != true) return;
        AddFiles(dlg.FileNames);
    }

    private void DoRemoveFile(string? filePath)
    {
        if (filePath is null || _isJobRunning) return;

        var item = Files.FirstOrDefault(f => f.FilePath == filePath);
        if (item is null) return;

        Files.Remove(item);
        RefreshFileState();
    }

    private void DoSelectFormat(string? ext)
    {
        if (ext is null || _isJobRunning) return;
        SelectedFormatExt = ext;
    }

    private async void DoConvert()
    {
        if (_isJobRunning)
        {
            DoCancel();
            return;
        }

        if (!CanConvert) return;

        var outputDir = PickOutputDirectory(out var singleOutputPath);
        if (outputDir is null && singleOutputPath is null) return;

        CancelPendingReset();

        _cts = new CancellationTokenSource();
        var token = _cts.Token;

        IsJobRunning = true;
        IsJobComplete = false;
        CompletedCount = 0;
        TotalCount = Files.Count;

        foreach (var f in Files)
            f.Status = CompressionFileStatus.Running;

        var files   = Files.ToList();
        var ext     = _selectedFormatExt;
        var quality = _applyCompression ? _qualityLevel : 4;
        var strip   = _stripMetadata;

        var results = new List<(string input, bool success, long outputBytes, string? error)>(files.Count);

        await Task.Run(() =>
        {
            foreach (var f in files)
            {
                if (token.IsCancellationRequested) break;

                string outPath;
                if (singleOutputPath is not null)
                {
                    outPath = singleOutputPath;
                }
                else
                {
                    var outName = Path.GetFileNameWithoutExtension(f.FilePath) + "." + ext;
                    outPath = Path.Combine(outputDir!, outName);
                }

                var result = _conversionService.Convert(f.FilePath, outPath, ext, quality, strip);
                long outBytes = result.Success ? GetFileSizeBytes(outPath) : 0;

                results.Add((f.FilePath, result.Success, outBytes, result.ErrorMessage));

                var localDone = results.Count;
                var localF    = f;
                var localOk   = result.Success;
                var localErr  = result.ErrorMessage;
                var localOut  = outBytes;

                Application.Current?.Dispatcher.InvokeAsync(() =>
                {
                    localF.OutputSizeBytes = localOut;
                    localF.ErrorMessage    = localErr;
                    localF.Status = localOk
                        ? CompressionFileStatus.Done
                        : CompressionFileStatus.Error;
                    localF.NotifyStatus();
                    CompletedCount = localDone;
                });
            }
        });

        IsJobRunning  = false;
        IsJobComplete = true;

        _cts?.Dispose();
        _cts = null;

        int done   = results.Count(r => r.success);
        int failed = results.Count - done;
        long totalOut = results.Sum(r => r.outputBytes);

        SummaryFilesSegment = failed == 0
            ? (done == 1
                ? _loc.Get("imgconv.summary_seg_files_one")
                : string.Format(_loc.Get("imgconv.summary_seg_files_many"), done))
            : string.Format(_loc.Get("imgconv.summary_seg_partial"), done, failed);

        SummarySizeSegment = string.Format(_loc.Get("imgconv.summary_seg_size"), FormatBytes(totalOut));

        ScheduleAutoReset(failed > 0);
    }

    private void DoCancel()
    {
        _cts?.Cancel();
    }

    private void ResetForNextJob()
    {
        CancelPendingReset();
        InternalReset();
    }

    // ── Output path resolution ───────────────────────────────────────────────

    private string? PickOutputDirectory(out string? singleOutputPath)
    {
        singleOutputPath = null;

        if (Files.Count == 1)
        {
            var inputPath  = Files[0].FilePath;
            var suggested  = Path.GetFileNameWithoutExtension(inputPath) + "." + _selectedFormatExt;
            var initialDir = Path.GetDirectoryName(inputPath) ?? string.Empty;
            var ext        = _selectedFormatExt;

            var filter = ext switch
            {
                "jpg"  => "JPEG image|*.jpg",
                "png"  => "PNG image|*.png",
                "webp" => "WebP image|*.webp",
                "avif" => "AVIF image|*.avif",
                "gif"  => "GIF image|*.gif",
                "bmp"  => "BMP image|*.bmp",
                "tif"  => "TIFF image|*.tif",
                "ico"  => "ICO image|*.ico",
                "tga"  => "TGA image|*.tga",
                _      => $"{ext.ToUpperInvariant()} image|*.{ext}",
            };

            var dlg = new Microsoft.Win32.SaveFileDialog
            {
                FileName         = suggested,
                InitialDirectory = initialDir,
                Filter           = filter,
            };
            if (dlg.ShowDialog() != true) return null;

            singleOutputPath = dlg.FileName;
            return null;
        }
        else
        {
            var dlg = new Microsoft.Win32.OpenFolderDialog
            {
                Title = "Select Output Folder",
            };
            if (dlg.ShowDialog() != true) return null;

            return dlg.FolderName;
        }
    }

    // ── Auto-reset ───────────────────────────────────────────────────────────

    private void ScheduleAutoReset(bool hasErrors)
    {
        CancelPendingReset();
        _resetTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(hasErrors ? 8 : 3) };
        _resetTimer.Tick += (_, _) =>
        {
            _resetTimer?.Stop();
            _resetTimer = null;
            InternalReset();
        };
        _resetTimer.Start();
    }

    private void CancelPendingReset()
    {
        _resetTimer?.Stop();
        _resetTimer = null;
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private void InternalReset()
    {
        Files.Clear();
        IsJobComplete = false;
        IsJobRunning  = false;
        CompletedCount = 0;
        TotalCount     = 0;
        SummaryFilesSegment = string.Empty;
        SummarySizeSegment  = string.Empty;
        RefreshFileState();
    }

    private void RefreshFileState()
    {
        OnPropertyChanged(nameof(HasFiles));
        OnPropertyChanged(nameof(ShowFileList));
        OnPropertyChanged(nameof(CanConvert));
        OnPropertyChanged(nameof(IsConvertButtonEnabled));
    }

    private void RefreshFormatFlags()
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
    }

    public void OnLanguageChanged()
    {
        OnPropertyChanged(nameof(DropLabel));
        OnPropertyChanged(nameof(BrowseLabel));
        OnPropertyChanged(nameof(FormatSectionLabel));
        OnPropertyChanged(nameof(QualitySectionLabel));
        OnPropertyChanged(nameof(LevelSmallestLabel));
        OnPropertyChanged(nameof(LevelLowLabel));
        OnPropertyChanged(nameof(LevelBalancedLabel));
        OnPropertyChanged(nameof(LevelHighLabel));
        OnPropertyChanged(nameof(LevelBestLabel));
        OnPropertyChanged(nameof(StripMetaLabel));
        OnPropertyChanged(nameof(CompressToggleLabel));
        OnPropertyChanged(nameof(RemoveFileLabel));
        OnPropertyChanged(nameof(ProgressTitleLabel));
        OnPropertyChanged(nameof(ProgressLabel));
        OnPropertyChanged(nameof(ConvertButtonLabel));
    }
}
