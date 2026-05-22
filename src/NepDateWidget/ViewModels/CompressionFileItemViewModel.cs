using NepDateWidget.Models;
using static NepDateWidget.Helpers.FileFormatHelper;

namespace NepDateWidget.ViewModels;

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
