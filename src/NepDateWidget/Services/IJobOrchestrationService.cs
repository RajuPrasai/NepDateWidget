using NepDateWidget.Models;

namespace NepDateWidget.Services;

public interface IJobOrchestrationService
{
    bool IsJobRunning { get; }

    /// <summary>
    /// Fired from a thread pool thread. Subscribers MUST marshal to UI thread
    /// via Application.Current.Dispatcher.InvokeAsync before touching bound properties.
    /// </summary>
    event EventHandler<JobProgressState> Progress;

    Task StartJobAsync(IReadOnlyList<CompressionJob> jobs, CancellationToken cancellationToken = default);

    /// <summary>
    /// Runs a batch of conversion jobs. The Kind discriminator on each descriptor
    /// determines which pipeline is used (image-to-image, PDF-to-image, image-to-PDF).
    /// </summary>
    Task StartConversionJobAsync(IReadOnlyList<ConversionJobDescriptor> jobs, CancellationToken cancellationToken = default);

    void CancelJob();
}
