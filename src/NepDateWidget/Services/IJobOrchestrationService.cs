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
    /// Runs a batch of format-conversion jobs (format change ± quality ± resize).
    /// Uses the same parallel semaphore infrastructure as StartJobAsync.
    /// </summary>
    Task StartConversionJobAsync(IReadOnlyList<ConversionJobDescriptor> jobs, CancellationToken cancellationToken = default);

    void CancelJob();
}
