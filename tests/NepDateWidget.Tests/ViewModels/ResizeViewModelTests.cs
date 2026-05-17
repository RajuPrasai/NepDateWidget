using NepDateWidget.Models;
using NepDateWidget.Services;
using NepDateWidget.Tests.Services;
using NepDateWidget.ViewModels;

namespace NepDateWidget.Tests.ViewModels;

/// <summary>
/// Tests for ResizeViewModel covering CanResize gating, AlsoCompress/ShowQualitySlider
/// toggle, QualityLevel clamping, WidthText/HeightText validation effects,
/// MixedTypeWarning on heterogeneous file additions, and file list deduplication.
///
/// Uses the real FileTypeService (deterministic extension→MIME lookup) and a no-op
/// fake orchestrator. File paths use fake extensions — the real service only inspects
/// the extension, not the file on disk.
/// </summary>
public class ResizeViewModelTests
{
    // ── Fake orchestrator ─────────────────────────────────────────────────────

    private sealed class FakeOrchestrator : IJobOrchestrationService
    {
        public bool IsJobRunning => false;
        public event EventHandler<JobProgressState>? Progress;
        public Task StartJobAsync(IReadOnlyList<CompressionJob> jobs, CancellationToken ct = default) => Task.CompletedTask;
        public void CancelJob() { }
        // Suppress unused event warning
        private void RaiseFakeProgress() => Progress?.Invoke(this, new JobProgressState());
    }

    private static ResizeViewModel Create()
    {
        var fileTypeSvc = new FileTypeService();
        var loc = new LocalizationService(TestPaths.DefaultLocalizationPath);
        loc.SetLanguage("en");
        return new ResizeViewModel(fileTypeSvc, new FakeOrchestrator(), loc);
    }

    // ── Initial state ─────────────────────────────────────────────────────────

    [Fact]
    public void Constructor_HasFiles_False()
    {
        var vm = Create();
        Assert.False(vm.HasFiles);
    }

    [Fact]
    public void Constructor_CanResize_False()
    {
        var vm = Create();
        Assert.False(vm.CanResize);
    }

    [Fact]
    public void Constructor_ShowQualitySlider_False()
    {
        var vm = Create();
        Assert.False(vm.ShowQualitySlider);
    }

    [Fact]
    public void Constructor_AlsoCompress_False()
    {
        var vm = Create();
        Assert.False(vm.AlsoCompress);
    }

    [Fact]
    public void Constructor_QualityLevel_Default_IsOne()
    {
        var vm = Create();
        Assert.Equal(1, vm.QualityLevel);
    }

    // ── AlsoCompress ↔ ShowQualitySlider ─────────────────────────────────────

    [Fact]
    public void AlsoCompress_SetTrue_ShowQualitySlider_BecomesTrue()
    {
        var vm = Create();
        vm.AlsoCompress = true;
        Assert.True(vm.ShowQualitySlider);
    }

    [Fact]
    public void AlsoCompress_SetFalse_ShowQualitySlider_BecomesFalse()
    {
        var vm = Create();
        vm.AlsoCompress = true;
        vm.AlsoCompress = false;
        Assert.False(vm.ShowQualitySlider);
    }

    [Fact]
    public void ShowQualitySlider_MirrorsAlsoCompress_Exactly()
    {
        var vm = Create();
        Assert.Equal(vm.AlsoCompress, vm.ShowQualitySlider);

        vm.AlsoCompress = true;
        Assert.Equal(vm.AlsoCompress, vm.ShowQualitySlider);

        vm.AlsoCompress = false;
        Assert.Equal(vm.AlsoCompress, vm.ShowQualitySlider);
    }

    // ── QualityLevel clamping ─────────────────────────────────────────────────

    [Fact]
    public void QualityLevel_AboveMax_ClampedToFour()
    {
        var vm = Create();
        vm.QualityLevel = 99;
        Assert.Equal(4, vm.QualityLevel);
    }

    [Fact]
    public void QualityLevel_BelowMin_ClampedToZero()
    {
        var vm = Create();
        vm.QualityLevel = -5;
        Assert.Equal(0, vm.QualityLevel);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    public void QualityLevel_ValidValues_StoredExactly(int level)
    {
        var vm = Create();
        vm.QualityLevel = level;
        Assert.Equal(level, vm.QualityLevel);
    }

    // ── AddFiles → HasFiles / CanResize ───────────────────────────────────────

    [Fact]
    public void AddFiles_WithJpeg_HasFiles_BecomesTrue()
    {
        var vm = Create();
        vm.AddFiles(["C:\\test\\photo.jpg"]);
        Assert.True(vm.HasFiles);
    }

    [Fact]
    public void AddFiles_WithJpeg_NoWidth_NoHeight_CanResize_False()
    {
        // CanResize requires at least one valid non-zero dimension
        var vm = Create();
        vm.AddFiles(["C:\\test\\photo.jpg"]);
        Assert.False(vm.CanResize);
    }

    [Fact]
    public void AddFiles_WithJpeg_WidthSetOnly_CanResize_True()
    {
        var vm = Create();
        vm.AddFiles(["C:\\test\\photo.jpg"]);
        vm.WidthText = "800";
        Assert.True(vm.CanResize);
    }

    [Fact]
    public void AddFiles_WithJpeg_HeightSetOnly_CanResize_True()
    {
        // Only one dimension is required for CanResize
        var vm = Create();
        vm.AddFiles(["C:\\test\\photo.jpg"]);
        vm.HeightText = "600";
        Assert.True(vm.CanResize);
    }

    [Fact]
    public void AddFiles_WithJpeg_BothDimensionsSet_CanResize_True()
    {
        var vm = Create();
        vm.AddFiles(["C:\\test\\photo.jpg"]);
        vm.WidthText = "800";
        vm.HeightText = "600";
        Assert.True(vm.CanResize);
    }

    // ── WidthText / HeightText validation ────────────────────────────────────

    [Fact]
    public void WidthText_NonNumeric_CanResize_False()
    {
        var vm = Create();
        vm.AddFiles(["C:\\test\\photo.jpg"]);
        vm.WidthText = "abc";
        // HeightText is empty — no valid dimension
        Assert.False(vm.CanResize);
    }

    [Fact]
    public void WidthText_Zero_CanResize_False_WithNoOtherDimension()
    {
        var vm = Create();
        vm.AddFiles(["C:\\test\\photo.jpg"]);
        vm.WidthText = "0";
        Assert.False(vm.CanResize);
    }

    [Fact]
    public void HeightText_Zero_WidthAlsoZero_CanResize_False()
    {
        var vm = Create();
        vm.AddFiles(["C:\\test\\photo.jpg"]);
        vm.WidthText = "0";
        vm.HeightText = "0";
        Assert.False(vm.CanResize);
    }

    [Fact]
    public void WidthText_Invalid_HeightValid_CanResize_True()
    {
        // One valid dimension is sufficient
        var vm = Create();
        vm.AddFiles(["C:\\test\\photo.jpg"]);
        vm.WidthText = "abc"; // invalid
        vm.HeightText = "600"; // valid
        Assert.True(vm.CanResize);
    }

    // ── MixedTypeWarning ──────────────────────────────────────────────────────

    [Fact]
    public void AddFiles_SameType_NoMixedTypeWarning()
    {
        var vm = Create();
        vm.AddFiles(["C:\\a.jpg"]);
        Assert.False(vm.HasMixedTypeWarning);
        Assert.Equal(string.Empty, vm.MixedTypeWarning);
    }

    [Fact]
    public void AddFiles_MixedJpegAndPng_SetsMixedTypeWarning()
    {
        // FileTypeService.ValidateSameType returns error for JPEG + PNG
        var vm = Create();
        vm.AddFiles(["C:\\a.jpg"]);
        vm.AddFiles(["C:\\b.png"]);

        Assert.True(vm.HasMixedTypeWarning);
        Assert.NotEmpty(vm.MixedTypeWarning);
    }

    [Fact]
    public void AddFiles_MixedTypeWarning_BlocksCanResize()
    {
        var vm = Create();
        vm.AddFiles(["C:\\a.jpg"]);
        vm.WidthText = "800";
        Assert.True(vm.CanResize); // precondition

        vm.AddFiles(["C:\\b.png"]);

        Assert.False(vm.CanResize);
    }

    [Fact]
    public void AddFiles_AllSameType_After_PreviousMixed_ClearsWarning()
    {
        // Can only clear warning by removing files; adding all same-type from scratch
        var vm = Create();
        vm.AddFiles(["C:\\a.jpg"]);
        vm.AddFiles(["C:\\b.png"]);
        Assert.True(vm.HasMixedTypeWarning); // precondition

        // Remove the PNG file to restore homogeneity
        vm.RemoveFileCommand.Execute("C:\\b.png");

        Assert.False(vm.HasMixedTypeWarning);
    }

    // ── File deduplication ────────────────────────────────────────────────────

    [Fact]
    public void AddFiles_SamePath_Twice_ResultsInOneEntry()
    {
        var vm = Create();
        vm.AddFiles(["C:\\test\\photo.jpg"]);
        vm.AddFiles(["C:\\test\\photo.jpg"]);

        Assert.Single(vm.Files);
    }

    [Fact]
    public void AddFiles_SamePath_CaseInsensitive_ResultsInOneEntry()
    {
        var vm = Create();
        vm.AddFiles(["C:\\test\\Photo.JPG"]);
        vm.AddFiles(["C:\\test\\photo.jpg"]);

        Assert.Single(vm.Files);
    }

    [Fact]
    public void AddFiles_SamePath_SecondAddResets_ToFileName()
    {
        // The replacement path may differ in casing; filename is re-read from path
        var vm = Create();
        vm.AddFiles(["C:\\test\\photo.jpg"]);
        var firstName = vm.Files[0].FileName;

        vm.AddFiles(["C:\\test\\photo.jpg"]);

        Assert.Equal(firstName, vm.Files[0].FileName);
        Assert.Single(vm.Files);
    }

    // ── RemoveFile ────────────────────────────────────────────────────────────

    [Fact]
    public void RemoveFile_KnownPath_RemovesFromList()
    {
        var vm = Create();
        vm.AddFiles(["C:\\a.jpg"]);
        vm.RemoveFileCommand.Execute("C:\\a.jpg");
        Assert.False(vm.HasFiles);
    }

    [Fact]
    public void RemoveFile_UnknownPath_NoOp()
    {
        var vm = Create();
        vm.AddFiles(["C:\\a.jpg"]);
        vm.RemoveFileCommand.Execute("C:\\nonexistent.jpg");
        Assert.Single(vm.Files);
    }

    [Fact]
    public void RemoveFile_Null_NoOp()
    {
        var vm = Create();
        vm.AddFiles(["C:\\a.jpg"]);
        vm.RemoveFileCommand.Execute(null);
        Assert.Single(vm.Files);
    }

    [Fact]
    public void RemoveFile_LastFile_CanResize_False()
    {
        var vm = Create();
        vm.AddFiles(["C:\\a.jpg"]);
        vm.WidthText = "800";
        Assert.True(vm.CanResize); // precondition

        vm.RemoveFileCommand.Execute("C:\\a.jpg");

        Assert.False(vm.CanResize);
    }

    // ── IsPdfLoaded blocks resize ─────────────────────────────────────────────

    [Fact]
    public void AddFiles_Pdf_IsPdfLoaded_True_CanResize_False()
    {
        var vm = Create();
        vm.AddFiles(["C:\\doc.pdf"]);
        vm.WidthText = "800";

        Assert.True(vm.IsPdfLoaded);
        Assert.False(vm.CanResize);
    }

    // ── Unsupported extension ignored ─────────────────────────────────────────

    [Fact]
    public void AddFiles_UnsupportedExtension_FileNotAdded()
    {
        var vm = Create();
        vm.AddFiles(["C:\\file.exe"]);
        Assert.False(vm.HasFiles);
    }

    // ── Job lifecycle: IsJobRunning / IsJobComplete / ShowSummary ─────────────

    [Fact]
    public void Constructor_IsJobRunning_False()
    {
        var vm = Create();
        Assert.False(vm.IsJobRunning);
    }

    [Fact]
    public void Constructor_IsJobComplete_False()
    {
        var vm = Create();
        Assert.False(vm.IsJobComplete);
    }

    [Fact]
    public void Constructor_ShowSummary_False()
    {
        var vm = Create();
        Assert.False(vm.ShowSummary);
    }

    [Fact]
    public void CompletedCount_StartsAtZero()
    {
        var vm = Create();
        Assert.Equal(0, vm.CompletedCount);
    }

    [Fact]
    public void TotalCount_StartsAtZero()
    {
        var vm = Create();
        Assert.Equal(0, vm.TotalCount);
    }

    [Fact]
    public void DismissSummaryCommand_NotNull()
    {
        var vm = Create();
        Assert.NotNull(vm.DismissSummaryCommand);
    }

    [Fact]
    public void DismissSummaryCommand_WhenJobComplete_ResetsJobCompleteToFalse()
    {
        var vm = Create();
        // DoResize opens a file picker dialog which cannot be driven in unit tests.
        // Set _isJobComplete directly via reflection to simulate a completed job.
        typeof(ResizeViewModel)
            .GetField("_isJobComplete", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
            .SetValue(vm, true);

        Assert.True(vm.IsJobComplete);

        vm.DismissSummaryCommand.Execute(null);

        Assert.False(vm.IsJobComplete);
        Assert.False(vm.ShowSummary);
    }
}
