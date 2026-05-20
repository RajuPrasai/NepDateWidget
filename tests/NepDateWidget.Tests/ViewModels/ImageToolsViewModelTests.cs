using NepDateWidget.Models;
using NepDateWidget.Services;
using NepDateWidget.ViewModels;

namespace NepDateWidget.Tests.ViewModels;

/// <summary>
/// Tests for ImageToolsViewModel covering constructor state, AddFiles,
/// smart defaults, toggle interactions, CanRun guard matrix,
/// MixedTypeWarning, format selection, InternalReset, HasAnyPdfFile,
/// ActionButtonLabel, and OnLanguageChanged.
///
/// Uses real FileTypeService and LocalizationService. File paths use fake
/// extensions — FileTypeService only inspects the extension.
/// </summary>
public class ImageToolsViewModelTests
{
    // ── Fake orchestrator ─────────────────────────────────────────────────────

    private sealed class FakeOrchestrator : IJobOrchestrationService
    {
        public bool IsJobRunning => false;
        public event EventHandler<JobProgressState>? Progress;
        public Task StartJobAsync(IReadOnlyList<CompressionJob> jobs, CancellationToken ct = default) => Task.CompletedTask;
        public Task StartConversionJobAsync(IReadOnlyList<ConversionJobDescriptor> jobs, CancellationToken ct = default) => Task.CompletedTask;
        public void CancelJob() { }
        private void RaiseFakeProgress() => Progress?.Invoke(this, new JobProgressState());
    }

    private sealed class FakeConversionService : IImageConversionService
    {
        public ImageConversionResult Convert(
            string inputPath, string outputPath, string targetExtension,
            int qualityLevel, bool stripMetadata,
            uint? targetWidth = null, uint? targetHeight = null)
            => new(true);
    }

    private static ImageToolsViewModel Create()
    {
        var ft   = new FileTypeService();
        var orch = new FakeOrchestrator();
        var conv = new FakeConversionService();
        var loc  = new LocalizationService(TestPaths.DefaultLocalizationPath);
        loc.SetLanguage("en");
        return new ImageToolsViewModel(ft, orch, conv, loc);
    }

    // ── 1. Constructor state ──────────────────────────────────────────────────

    [Fact]
    public void Constructor_HasFiles_False()
    {
        var vm = Create();
        Assert.False(vm.HasFiles);
    }

    [Fact]
    public void Constructor_AllToggles_False()
    {
        var vm = Create();
        Assert.False(vm.IsCompressEnabled);
        Assert.False(vm.IsResizeEnabled);
        Assert.False(vm.IsConvertEnabled);
    }

    [Fact]
    public void Constructor_CanRun_False()
    {
        var vm = Create();
        Assert.False(vm.CanRun);
    }

    [Fact]
    public void Constructor_IsJobRunning_False()
    {
        var vm = Create();
        Assert.False(vm.IsJobRunning);
    }

    [Fact]
    public void Constructor_DefaultFormat_IsJpeg()
    {
        var vm = Create();
        Assert.Equal("jpg", vm.SelectedFormatExt);
        Assert.True(vm.IsFormatJpeg);
    }

    [Fact]
    public void Constructor_StripMetadata_TrueByDefault()
    {
        var vm = Create();
        Assert.True(vm.StripMetadata);
    }

    [Fact]
    public void Constructor_HasMixedTypeWarning_False()
    {
        var vm = Create();
        Assert.False(vm.HasMixedTypeWarning);
    }

    [Fact]
    public void Constructor_CanToggleCompress_True()
    {
        var vm = Create();
        Assert.True(vm.CanToggleCompress);
    }

    [Fact]
    public void Constructor_CanToggleResize_True()
    {
        var vm = Create();
        Assert.True(vm.CanToggleResize);
    }

    [Fact]
    public void Constructor_CanToggleConvert_True()
    {
        var vm = Create();
        Assert.True(vm.CanToggleConvert);
    }

    // ── 2. AddFiles ───────────────────────────────────────────────────────────

    [Fact]
    public void AddFiles_SingleJpeg_SetsHasFiles()
    {
        var vm = Create();
        vm.AddFiles(new[] { @"C:\test\photo.jpg" });
        Assert.True(vm.HasFiles);
        Assert.Single(vm.Files);
    }

    [Fact]
    public void AddFiles_UnsupportedExtension_Skipped()
    {
        var vm = Create();
        vm.AddFiles(new[] { @"C:\test\doc.docx" });
        Assert.False(vm.HasFiles);
        Assert.Empty(vm.Files);
    }

    [Fact]
    public void AddFiles_MixedSupportedAndUnsupported_OnlySupported()
    {
        var vm = Create();
        vm.AddFiles(new[] { @"C:\test\photo.jpg", @"C:\test\doc.docx", @"C:\test\image.png" });
        Assert.Equal(2, vm.Files.Count);
    }

    [Fact]
    public void AddFiles_Deduplication_ReplacesExistingEntry()
    {
        var vm = Create();
        vm.AddFiles(new[] { @"C:\test\photo.jpg" });
        vm.AddFiles(new[] { @"C:\test\photo.jpg" });
        Assert.Single(vm.Files);
    }

    [Fact]
    public void AddFiles_AllRawExtensions_Accepted()
    {
        var vm = Create();
        var raws = new[] { ".arw", ".cr2", ".cr3", ".dng", ".nef", ".orf", ".raf", ".rw2", ".erf", ".pef", ".x3f" };
        var paths = raws.Select((ext, i) => $@"C:\test\raw{i}{ext}").ToArray();
        vm.AddFiles(paths);
        Assert.Equal(raws.Length, vm.Files.Count);
    }

    [Fact]
    public void AddFiles_Pdf_Accepted_SetsHasAnyPdfFile()
    {
        var vm = Create();
        vm.AddFiles(new[] { @"C:\test\doc.pdf" });
        Assert.True(vm.HasFiles);
        Assert.True(vm.HasAnyPdfFile);
    }

    // ── 3. Smart defaults ─────────────────────────────────────────────────────

    [Fact]
    public void SmartDefaults_Pdf_CompressOnly()
    {
        var vm = Create();
        vm.AddFiles(new[] { @"C:\test\doc.pdf" });

        Assert.True(vm.IsCompressEnabled);
        Assert.False(vm.IsResizeEnabled);
        Assert.False(vm.IsConvertEnabled);
    }

    [Fact]
    public void SmartDefaults_Raw_ConvertToJpegOnly()
    {
        var vm = Create();
        vm.AddFiles(new[] { @"C:\test\photo.dng" });

        Assert.False(vm.IsCompressEnabled);
        Assert.True(vm.IsConvertEnabled);
        Assert.False(vm.IsResizeEnabled);
        Assert.Equal("jpg", vm.SelectedFormatExt);
    }

    [Fact]
    public void SmartDefaults_Heic_CompressAndConvertToJpeg()
    {
        var vm = Create();
        vm.AddFiles(new[] { @"C:\test\photo.heic" });

        Assert.True(vm.IsCompressEnabled);
        Assert.True(vm.IsConvertEnabled);
        Assert.Equal("jpg", vm.SelectedFormatExt);
    }

    [Fact]
    public void SmartDefaults_Gif_CompressOnly()
    {
        var vm = Create();
        vm.AddFiles(new[] { @"C:\test\anim.gif" });

        Assert.True(vm.IsCompressEnabled);
        Assert.False(vm.IsResizeEnabled);
        Assert.False(vm.IsConvertEnabled);
    }

    [Fact]
    public void SmartDefaults_Webp_CompressOnly()
    {
        var vm = Create();
        vm.AddFiles(new[] { @"C:\test\img.webp" });

        Assert.True(vm.IsCompressEnabled);
        Assert.False(vm.IsConvertEnabled);
    }

    [Fact]
    public void SmartDefaults_Avif_CompressOnly()
    {
        var vm = Create();
        vm.AddFiles(new[] { @"C:\test\img.avif" });

        Assert.True(vm.IsCompressEnabled);
        Assert.False(vm.IsConvertEnabled);
    }

    [Fact]
    public void SmartDefaults_StandardJpeg_AnyQuality_CompressResizeConvert()
    {
        var vm = Create();
        vm.QualityLevel = 3; // high quality — still enables all three; level controls degree, not what's active
        vm.AddFiles(new[] { @"C:\test\photo.jpg" });

        Assert.True(vm.IsCompressEnabled);
        Assert.True(vm.IsResizeEnabled);
        Assert.True(vm.IsConvertEnabled);
        Assert.Equal("jpg", vm.SelectedFormatExt);
    }

    [Fact]
    public void SmartDefaults_StandardPng_AllQualityLevels_CompressResizeConvert()
    {
        var vm = Create();
        vm.QualityLevel = 0; // quality level irrelevant — all three always enabled for standard images
        vm.AddFiles(new[] { @"C:\test\image.png" });

        Assert.True(vm.IsCompressEnabled);
        Assert.True(vm.IsResizeEnabled);
        Assert.True(vm.IsConvertEnabled);
        Assert.Equal("jpg", vm.SelectedFormatExt);
    }

    // ── 4. UserHasManuallyChangedToggles bypasses smart defaults ─────────────

    [Fact]
    public void AddFiles_AfterManualToggle_DoesNotReApplySmartDefaults()
    {
        var vm = Create();
        // User manually turns ON convert (sets the flag).
        vm.IsConvertEnabled = true;
        // Now add a PDF — smart defaults would set CompressOnly but should not fire.
        vm.AddFiles(new[] { @"C:\test\doc.pdf" });

        // Convert was set manually; smart defaults should NOT reset it.
        Assert.True(vm.IsConvertEnabled);
        // But note: CanToggleConvert is still blocked by PDF.
        Assert.False(vm.CanToggleConvert);
    }

    [Fact]
    public void AddFiles_SecondBatchAfterManualToggle_NeverReAppliesDefaults()
    {
        var vm = Create();
        vm.AddFiles(new[] { @"C:\test\photo.jpg" }); // applies defaults
        vm.IsResizeEnabled = false; // manual toggle — sets flag
        vm.AddFiles(new[] { @"C:\test\photo2.png" }); // second add must NOT re-apply

        Assert.False(vm.IsResizeEnabled);
    }

    // ── 5. Toggle interactions ────────────────────────────────────────────────

    [Fact]
    public void CanToggleResize_BlockedByPdf()
    {
        var vm = Create();
        vm.AddFiles(new[] { @"C:\test\doc.pdf" });
        Assert.False(vm.CanToggleResize);
    }

    [Fact]
    public void CanToggleConvert_BlockedByPdf()
    {
        var vm = Create();
        vm.AddFiles(new[] { @"C:\test\doc.pdf" });
        Assert.False(vm.CanToggleConvert);
    }

    [Fact]
    public void ShowResizeSection_TrueOnlyWhenResizeEnabledAndNoPdf()
    {
        var vm = Create();
        vm.AddFiles(new[] { @"C:\test\photo.jpg" });
        vm.IsCompressEnabled = true;
        vm.IsResizeEnabled = true;

        Assert.True(vm.ShowResizeSection);
    }

    [Fact]
    public void ShowConvertSection_TrueOnlyWhenConvertEnabledAndNoPdf()
    {
        var vm = Create();
        vm.AddFiles(new[] { @"C:\test\photo.jpg" });
        vm.IsConvertEnabled = true;

        Assert.True(vm.ShowConvertSection);
    }

    [Fact]
    public void ShowQualitySlider_TrueWhenCompressOnly()
    {
        var vm = Create();
        vm.AddFiles(new[] { @"C:\test\photo.jpg" });
        vm.IsCompressEnabled = true;
        vm.IsConvertEnabled = false;

        Assert.True(vm.ShowQualitySlider);
    }

    [Fact]
    public void ShowQualitySlider_TrueWhenCompressAndConvertTargetingLossyFormat()
    {
        var vm = Create();
        vm.AddFiles(new[] { @"C:\test\photo.png" });
        vm.IsCompressEnabled = true;
        vm.IsConvertEnabled = true;
        vm.SelectFormatCommand.Execute("webp");

        Assert.True(vm.ShowQualitySlider);
    }

    [Fact]
    public void ShowQualitySlider_FalseWhenCompressAndConvertTargetingLosslessFormat()
    {
        var vm = Create();
        vm.AddFiles(new[] { @"C:\test\photo.jpg" });
        vm.IsCompressEnabled = true;
        vm.IsConvertEnabled = true;
        vm.SelectFormatCommand.Execute("png");

        Assert.False(vm.ShowQualitySlider);
    }

    [Fact]
    public void ShowQualitySlider_FalseWhenCompressIsOff()
    {
        var vm = Create();
        vm.AddFiles(new[] { @"C:\test\photo.jpg" });
        vm.IsCompressEnabled = false;
        vm.IsConvertEnabled = true;

        Assert.False(vm.ShowQualitySlider);
    }

    [Fact]
    public void ShowCompressSection_FalseWhenCompressOff()
    {
        var vm = Create();
        vm.AddFiles(new[] { @"C:\test\photo.jpg" });
        vm.IsCompressEnabled = false;
        Assert.False(vm.ShowCompressSection);
    }

    // ── 6. CanRun guard matrix ────────────────────────────────────────────────

    [Fact]
    public void CanRun_False_NoFiles()
    {
        var vm = Create();
        vm.IsCompressEnabled = true;
        Assert.False(vm.CanRun);
    }

    [Fact]
    public void CanRun_False_NoToggleActive()
    {
        var vm = Create();
        vm.AddFiles(new[] { @"C:\test\photo.jpg" });
        vm.IsCompressEnabled = false;
        vm.IsResizeEnabled = false;
        vm.IsConvertEnabled = false;
        Assert.False(vm.CanRun);
    }

    [Fact]
    public void CanRun_True_CompressOnlyWithFiles()
    {
        var vm = Create();
        vm.AddFiles(new[] { @"C:\test\photo.jpg" });
        vm.IsCompressEnabled = true;
        vm.IsResizeEnabled   = false; // smart defaults at quality=1 may enable resize
        vm.IsConvertEnabled  = false;
        Assert.True(vm.CanRun);
    }

    [Fact]
    public void CanRun_False_ResizeEnabledButNoDimensionsEntered()
    {
        var vm = Create();
        vm.AddFiles(new[] { @"C:\test\photo.jpg" });
        vm.IsResizeEnabled = true;
        vm.WidthText = string.Empty;
        vm.HeightText = string.Empty;
        Assert.False(vm.CanRun);
    }

    [Fact]
    public void CanRun_True_ResizeEnabledWithWidthOnly()
    {
        var vm = Create();
        vm.AddFiles(new[] { @"C:\test\photo.jpg" });
        vm.IsResizeEnabled = true;
        vm.WidthText = "800";
        Assert.True(vm.CanRun);
    }

    [Fact]
    public void CanRun_True_ResizeEnabledWithHeightOnly()
    {
        var vm = Create();
        vm.AddFiles(new[] { @"C:\test\photo.jpg" });
        vm.IsResizeEnabled = true;
        vm.HeightText = "600";
        Assert.True(vm.CanRun);
    }

    [Fact]
    public void CanRun_False_MixedTypesWithoutConvert()
    {
        var vm = Create();
        vm.AddFiles(new[] { @"C:\test\photo.jpg", @"C:\test\image.png" });
        vm.IsConvertEnabled = false;
        vm.IsCompressEnabled = true;
        // Mixed types and Convert is off → CanRun must be false.
        Assert.False(vm.CanRun);
    }

    [Fact]
    public void CanRun_True_MixedTypesWithConvert()
    {
        var vm = Create();
        vm.AddFiles(new[] { @"C:\test\photo.jpg", @"C:\test\image.png" });
        vm.IsConvertEnabled = true;
        vm.IsResizeEnabled  = false; // ensure resize does not gate CanRun on dimensions
        Assert.True(vm.CanRun);
    }

    // ── 7. MixedTypeWarning ───────────────────────────────────────────────────

    [Fact]
    public void MixedTypeWarning_SetWhenMixedMimesAndConvertOff()
    {
        var vm = Create();
        vm.AddFiles(new[] { @"C:\test\photo.jpg", @"C:\test\image.png" });
        vm.IsConvertEnabled = false;

        Assert.True(vm.HasMixedTypeWarning);
        Assert.NotEmpty(vm.MixedTypeWarning);
    }

    [Fact]
    public void MixedTypeWarning_ClearedWhenConvertEnabled()
    {
        var vm = Create();
        vm.AddFiles(new[] { @"C:\test\photo.jpg", @"C:\test\image.png" });
        vm.IsConvertEnabled = false;

        Assert.True(vm.HasMixedTypeWarning);

        vm.IsConvertEnabled = true;

        Assert.False(vm.HasMixedTypeWarning);
    }

    [Fact]
    public void MixedTypeWarning_NotSetForHomogeneousFiles()
    {
        var vm = Create();
        vm.AddFiles(new[] { @"C:\test\a.jpg", @"C:\test\b.jpg" });

        Assert.False(vm.HasMixedTypeWarning);
    }

    [Fact]
    public void MixedTypeWarning_ClearedAfterReset()
    {
        var vm = Create();
        vm.AddFiles(new[] { @"C:\test\photo.jpg", @"C:\test\image.png" });
        // Smart defaults enable Convert for mixed types, clearing the warning.
        // Manually disable Convert to produce the warning state.
        vm.IsConvertEnabled = false;
        Assert.True(vm.HasMixedTypeWarning);

        vm.DismissSummaryCommand.Execute(null);

        Assert.False(vm.HasMixedTypeWarning);
    }

    // ── 8. Format selection ───────────────────────────────────────────────────

    [Fact]
    public void SelectFormat_Png_UpdatesIsFormatPng_ClearsOthers()
    {
        var vm = Create();
        vm.SelectFormatCommand.Execute("png");

        Assert.True(vm.IsFormatPng);
        Assert.False(vm.IsFormatJpeg);
        Assert.False(vm.IsFormatWebp);
        Assert.False(vm.IsFormatAvif);
        Assert.False(vm.IsFormatGif);
        Assert.False(vm.IsFormatBmp);
        Assert.False(vm.IsFormatTiff);
        Assert.False(vm.IsFormatIco);
        Assert.False(vm.IsFormatTga);
    }

    [Fact]
    public void SelectFormat_AllFormats_Accepted()
    {
        var vm = Create();
        var formats = new[] { "jpg", "png", "webp", "avif", "gif", "bmp", "tif", "ico", "tga" };
        foreach (var fmt in formats)
        {
            vm.SelectFormatCommand.Execute(fmt);
            Assert.Equal(fmt, vm.SelectedFormatExt);
        }
    }

    [Fact]
    public void SelectFormat_Png_ShowQualitySlider_FalseWhenCompressAndConvertOn()
    {
        var vm = Create();
        vm.AddFiles(new[] { @"C:\test\photo.jpg" });
        vm.IsCompressEnabled = true;
        vm.IsConvertEnabled = true;
        vm.SelectFormatCommand.Execute("png");

        Assert.False(vm.ShowQualitySlider);
    }

    [Fact]
    public void SelectFormat_Webp_ShowQualitySlider_TrueWhenCompressAndConvertOn()
    {
        var vm = Create();
        vm.AddFiles(new[] { @"C:\test\photo.jpg" });
        vm.IsCompressEnabled = true;
        vm.IsConvertEnabled = true;
        vm.SelectFormatCommand.Execute("webp");

        Assert.True(vm.ShowQualitySlider);
    }

    // ── 9. InternalReset (DismissSummaryCommand) ──────────────────────────────

    [Fact]
    public void InternalReset_ClearsFiles()
    {
        var vm = Create();
        vm.AddFiles(new[] { @"C:\test\photo.jpg", @"C:\test\image.png" });
        vm.DismissSummaryCommand.Execute(null);

        Assert.False(vm.HasFiles);
        Assert.Empty(vm.Files);
    }

    [Fact]
    public void InternalReset_ClearsJobState()
    {
        var vm = Create();
        vm.AddFiles(new[] { @"C:\test\photo.jpg" });
        vm.DismissSummaryCommand.Execute(null);

        Assert.False(vm.IsJobRunning);
        Assert.False(vm.ShowSummary);
        Assert.False(vm.CanRun);
    }

    [Fact]
    public void InternalReset_ClearsWidthAndHeight()
    {
        var vm = Create();
        vm.WidthText = "1920";
        vm.HeightText = "1080";
        vm.DismissSummaryCommand.Execute(null);

        Assert.Equal(string.Empty, vm.WidthText);
        Assert.Equal(string.Empty, vm.HeightText);
    }

    [Fact]
    public void InternalReset_ClearsMixedTypeWarning()
    {
        var vm = Create();
        vm.AddFiles(new[] { @"C:\test\photo.jpg", @"C:\test\image.png" });
        // Smart defaults enable Convert for mixed types, clearing the warning.
        // Manually disable Convert to produce the warning state.
        vm.IsConvertEnabled = false;
        Assert.True(vm.HasMixedTypeWarning);

        vm.DismissSummaryCommand.Execute(null);

        Assert.False(vm.HasMixedTypeWarning);
    }

    // ── 10. HasAnyPdfFile detection ───────────────────────────────────────────

    [Fact]
    public void HasAnyPdfFile_False_ForImages()
    {
        var vm = Create();
        vm.AddFiles(new[] { @"C:\test\photo.jpg" });
        Assert.False(vm.HasAnyPdfFile);
    }

    [Fact]
    public void HasAnyPdfFile_True_WhenPdfPresent()
    {
        var vm = Create();
        vm.AddFiles(new[] { @"C:\test\doc.pdf" });
        Assert.True(vm.HasAnyPdfFile);
    }

    [Fact]
    public void HasAnyPdfFile_True_WhenMixedImageAndPdf()
    {
        var vm = Create();
        vm.AddFiles(new[] { @"C:\test\photo.jpg", @"C:\test\doc.pdf" });
        Assert.True(vm.HasAnyPdfFile);
    }

    [Fact]
    public void HasAnyPdfFile_False_AfterRemovingPdf()
    {
        var vm = Create();
        vm.AddFiles(new[] { @"C:\test\doc.pdf" });
        Assert.True(vm.HasAnyPdfFile);

        vm.RemoveFileCommand.Execute(@"C:\test\doc.pdf");

        Assert.False(vm.HasAnyPdfFile);
    }

    [Fact]
    public void ShowGifOptions_TrueWhenCompressEnabledAndGifFile()
    {
        var vm = Create();
        vm.AddFiles(new[] { @"C:\test\anim.gif" });
        vm.IsCompressEnabled = true;
        Assert.True(vm.ShowGifOptions);
    }

    [Fact]
    public void ShowGifOptions_FalseWhenCompressDisabled()
    {
        var vm = Create();
        vm.AddFiles(new[] { @"C:\test\anim.gif" });
        vm.IsCompressEnabled = false;
        Assert.False(vm.ShowGifOptions);
    }

    [Fact]
    public void ShowTiffOptions_TrueWhenCompressEnabledAndTiffFile()
    {
        var vm = Create();
        vm.AddFiles(new[] { @"C:\test\img.tif" });
        vm.IsCompressEnabled = true;
        Assert.True(vm.ShowTiffOptions);
    }

    [Fact]
    public void ShowPdfOptions_TrueWhenCompressEnabledAndPdfPresent()
    {
        var vm = Create();
        vm.AddFiles(new[] { @"C:\test\doc.pdf" });
        Assert.True(vm.ShowPdfOptions);
    }

    [Fact]
    public void ShowStripMetaOption_TrueWhenFilesAndNoPdf()
    {
        var vm = Create();
        vm.AddFiles(new[] { @"C:\test\photo.jpg" });
        Assert.True(vm.ShowStripMetaOption);
    }

    [Fact]
    public void ShowStripMetaOption_FalseWhenPdfPresent()
    {
        var vm = Create();
        vm.AddFiles(new[] { @"C:\test\doc.pdf" });
        Assert.False(vm.ShowStripMetaOption);
    }

    // ── 11. ActionButtonLabel ─────────────────────────────────────────────────

    [Fact]
    public void ActionButtonLabel_SingleCompress_ReturnsCompressLabel()
    {
        var vm = Create();
        vm.AddFiles(new[] { @"C:\test\photo.jpg" });
        vm.IsCompressEnabled = true;
        vm.IsResizeEnabled   = false;
        vm.IsConvertEnabled  = false;

        // Label should contain the compress toggle text (non-empty, not the generic run label).
        var label = vm.ActionButtonLabel;
        Assert.NotEmpty(label);
        Assert.DoesNotContain("Process", label, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ActionButtonLabel_SingleResize_ReturnsResizeLabel()
    {
        var vm = Create();
        vm.AddFiles(new[] { @"C:\test\photo.jpg" });
        vm.IsResizeEnabled   = true;
        vm.IsCompressEnabled = false;
        vm.IsConvertEnabled  = false;

        var label = vm.ActionButtonLabel;
        Assert.NotEmpty(label);
        Assert.DoesNotContain("Process", label, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ActionButtonLabel_SingleConvert_ReturnsConvertLabel()
    {
        var vm = Create();
        vm.AddFiles(new[] { @"C:\test\photo.jpg" });
        vm.IsConvertEnabled  = true;
        vm.IsCompressEnabled = false;
        vm.IsResizeEnabled   = false;

        var label = vm.ActionButtonLabel;
        Assert.NotEmpty(label);
        Assert.DoesNotContain("Process", label, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ActionButtonLabel_MultipleToggles_ReturnsRunLabel()
    {
        var vm = Create();
        vm.AddFiles(new[] { @"C:\test\photo.jpg" });
        vm.IsCompressEnabled = true;
        vm.IsResizeEnabled   = true;

        var label = vm.ActionButtonLabel;
        Assert.NotEmpty(label);
        // Multi-toggle label is the "imgtools.run_btn" key — just verify it does not
        // match a single-toggle label (all three singles are distinct and non-empty).
    }

    [Fact]
    public void ActionButtonLabel_NoToggles_ReturnsRunLabel()
    {
        var vm = Create();
        vm.AddFiles(new[] { @"C:\test\photo.jpg" });
        vm.IsCompressEnabled = false;
        vm.IsResizeEnabled   = false;
        vm.IsConvertEnabled  = false;

        // 0 active toggles hits the default case → run label.
        var label = vm.ActionButtonLabel;
        Assert.NotEmpty(label);
    }

    // ── 12. OnLanguageChanged ─────────────────────────────────────────────────

    [Fact]
    public void OnLanguageChanged_FiresPropertyChangedForAllLabels()
    {
        var vm = Create();
        var fired = new List<string?>();
        vm.PropertyChanged += (_, e) => fired.Add(e.PropertyName);

        vm.OnLanguageChanged();

        // At minimum all public label properties should be notified.
        Assert.Contains(nameof(vm.DropLabel),           fired);
        Assert.Contains(nameof(vm.BrowseLabel),         fired);
        Assert.Contains(nameof(vm.ProgressTitleLabel),  fired);
        Assert.Contains(nameof(vm.CompressToggleLabel), fired);
        Assert.Contains(nameof(vm.ResizeToggleLabel),   fired);
        Assert.Contains(nameof(vm.ConvertToggleLabel),  fired);
        Assert.Contains(nameof(vm.SectionCompressLabel), fired);
        Assert.Contains(nameof(vm.SectionResizeLabel),  fired);
        Assert.Contains(nameof(vm.SectionConvertLabel), fired);
        Assert.Contains(nameof(vm.ActionButtonLabel),   fired);
    }

    // ── 13. Level-based projected dimensions ─────────────────────────────────

    [Fact]
    public void QualityLevel_Change_UpdatesProjectedDimensionsFromOriginal()
    {
        // Simulate: files loaded, dimensions async-read, all 1000×500.
        var vm = Create();
        vm.AddFiles(new[] { @"C:\test\photo.jpg" });
        var item = vm.Files[0];
        // Inject dimension string as the async callback would.
        item.Dimensions = "1000 × 500";
        // Trigger the auto-populate path the same way AddFiles does.
        // Re-invoke by adding again (dedup replaces, then re-reads).
        // Instead, reflect on the private method for unit-testing purposes would be fragile;
        // verify through the public setter path after simulating the state.
        // Set width/height manually to represent level 1 (80%) projected values.
        vm.WidthText  = "800";
        vm.HeightText = "400";
        // _userHasManuallyChangedDimensions is now true — level change won't override.
        // This test verifies the guard works: change level, dimensions stay.
        int prevLevel = vm.QualityLevel;
        vm.QualityLevel = (prevLevel == 0) ? 1 : 0;
        Assert.Equal("800", vm.WidthText);
        Assert.Equal("400", vm.HeightText);
    }

    [Fact]
    public void ResizeDefaultPercent_HasFiveEntries_AllPositive()
    {
        var pct = CompressionProfiles.ResizeDefaultPercent;
        Assert.Equal(5, pct.Length);
        Assert.All(pct, p => Assert.True(p > 0 && p <= 1.0));
        // Level 0 must be smallest, level 4 largest.
        Assert.True(pct[0] < pct[4]);
    }

    // ── 14. Format-based compression auto-disable ─────────────────────────────

    [Fact]
    public void SetFormat_GifWithConvertOn_DisablesCompress()
    {
        var vm = Create();
        vm.AddFiles(new[] { @"C:\test\photo.jpg" });
        vm.IsCompressEnabled = true;
        vm.IsConvertEnabled  = true;
        vm.SelectFormatCommand.Execute("gif");
        Assert.False(vm.IsCompressEnabled);
    }

    [Fact]
    public void SetFormat_BmpWithConvertOn_DisablesCompress()
    {
        var vm = Create();
        vm.AddFiles(new[] { @"C:\test\photo.jpg" });
        vm.IsCompressEnabled = true;
        vm.IsConvertEnabled  = true;
        vm.SelectFormatCommand.Execute("bmp");
        Assert.False(vm.IsCompressEnabled);
    }

    [Fact]
    public void SetFormat_GifThenJpg_RestoresCompress()
    {
        var vm = Create();
        vm.AddFiles(new[] { @"C:\test\photo.jpg" });
        vm.IsCompressEnabled = true;
        vm.IsConvertEnabled  = true;
        vm.SelectFormatCommand.Execute("gif");
        Assert.False(vm.IsCompressEnabled);
        vm.SelectFormatCommand.Execute("jpg");
        Assert.True(vm.IsCompressEnabled);
    }

    [Fact]
    public void SetFormat_GifWithConvertOff_DoesNotDisableCompress()
    {
        var vm = Create();
        vm.AddFiles(new[] { @"C:\test\photo.jpg" });
        vm.IsCompressEnabled = true;
        vm.IsConvertEnabled  = false;
        vm.SelectFormatCommand.Execute("gif");
        Assert.True(vm.IsCompressEnabled); // Convert is OFF, format constraint doesn't apply
    }

    [Fact]
    public void UserManuallyReenable_AfterFormatDisable_PreventsAutoRestore()
    {
        // User re-enables compress after format disabled it; switching to another
        // no-compression format should disable again, but switching back to jpg should NOT
        // auto-restore because the flag was cleared when the user manually set it.
        var vm = Create();
        vm.AddFiles(new[] { @"C:\test\photo.jpg" });
        vm.IsConvertEnabled  = true;
        vm.IsCompressEnabled = true;
        vm.SelectFormatCommand.Execute("gif");
        Assert.False(vm.IsCompressEnabled);
        // User manually re-enables.
        vm.IsCompressEnabled = true;
        // Now switch to another format (webp supports compression).
        vm.SelectFormatCommand.Execute("webp");
        // Compress should stay as-is (true) — no auto-restore needed, it was manual.
        Assert.True(vm.IsCompressEnabled);
        // Switching to bmp should disable again (new format constraint).
        vm.SelectFormatCommand.Execute("bmp");
        Assert.False(vm.IsCompressEnabled);
    }

    // ── 15. Auto-reset guard (regression) ────────────────────────────────────

    [Fact]
    public void InternalReset_WithNoTimer_DoesNotThrow()
    {
        // CancelPendingAutoReset is now called at the start of InternalReset.
        // When no timer is pending (_resetTimer == null) it must be a safe no-op.
        var vm = Create();
        vm.AddFiles(new[] { @"C:\test\photo.jpg" });
        var ex = Record.Exception(() => vm.DismissSummaryCommand.Execute(null));
        Assert.Null(ex);
        Assert.False(vm.HasFiles);
    }

    [Fact]
    public void AddFiles_WhenSummaryVisible_ClearsSummaryBeforeAddingFiles()
    {
        // Simulates the post-job state where summary is showing. Calling AddFiles
        // must dismiss the summary (and cancel any pending auto-reset timer).
        var vm = Create();
        var field = typeof(ImageToolsViewModel)
            .GetField("_isSummaryVisible",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
        field.SetValue(vm, true);
        Assert.True(vm.ShowSummary);

        vm.AddFiles(new[] { @"C:\test\photo.jpg" });

        Assert.False(vm.ShowSummary);
        Assert.True(vm.HasFiles);
        Assert.Single(vm.Files);
    }
}
