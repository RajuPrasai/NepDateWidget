using NepDateWidget.Models;
using System.IO;

namespace NepDateWidget.Services;

public sealed class JobOrchestrationService : IJobOrchestrationService
{
    private readonly IImageCompressionService _imageService;
    private readonly IPdfCompressionService _pdfService;
    private readonly IImageConversionService _conversionService;
    private readonly IPdfTranscodeService _pdfTranscode;

    private CancellationTokenSource? _cts;
    private volatile bool _isJobRunning;

    public bool IsJobRunning => _isJobRunning;

    public event EventHandler<JobProgressState>? Progress;

    public JobOrchestrationService(
        IImageCompressionService imageService,
        IPdfCompressionService pdfService,
        IImageConversionService conversionService,
        IPdfTranscodeService pdfTranscode)
    {
        _imageService = imageService ?? throw new ArgumentNullException(nameof(imageService));
        _pdfService = pdfService ?? throw new ArgumentNullException(nameof(pdfService));
        _conversionService = conversionService ?? throw new ArgumentNullException(nameof(conversionService));
        _pdfTranscode = pdfTranscode ?? throw new ArgumentNullException(nameof(pdfTranscode));
    }

    public async Task StartJobAsync(IReadOnlyList<CompressionJob> jobs, CancellationToken cancellationToken = default)
    {
        if (_isJobRunning)
        {
            return;
        }

        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _isJobRunning = true;

        // Pre-job: resolve output path collisions before any file is written.
        ResolveCollisions(jobs);

        int parallelism = Math.Max(1, Environment.ProcessorCount - 2);
        var semaphore = new SemaphoreSlim(parallelism, parallelism);
        int completed = 0;
        long totalSaved = 0;
        var token = _cts.Token;

        try
        {
            var tasks = jobs.Select(async job =>
            {
                await semaphore.WaitAsync(token).ConfigureAwait(false);
                try
                {
                    if (token.IsCancellationRequested)
                    {
                        return;
                    }

                    Progress?.Invoke(this, new JobProgressState
                    {
                        CompletedCount = completed,
                        TotalCount = jobs.Count,
                        CurrentFileName = Path.GetFileName(job.InputPath),
                        TotalSavedBytes = totalSaved,
                    });

                    CompressionResult result = await Task.Run(() =>
                    {
                        return job.Category == FileCategory.Pdf
                            ? _pdfService.Compress(job.InputPath, job.OutputPath, job.Settings)
                            : _imageService.Compress(job.InputPath, job.OutputPath, job.MimeType, job.Settings);
                    }, token).ConfigureAwait(false);

                    Interlocked.Add(ref totalSaved, Math.Max(0L, result.SavedBytes));
                    Interlocked.Increment(ref completed);

                    Progress?.Invoke(this, new JobProgressState
                    {
                        CompletedCount = completed,
                        TotalCount = jobs.Count,
                        CurrentFileName = Path.GetFileName(job.InputPath),
                        TotalSavedBytes = totalSaved,
                    });
                }
                finally
                {
                    semaphore.Release();
                }
            }).ToList();

            await Task.WhenAll(tasks).ConfigureAwait(false);
        }
        finally
        {
            _isJobRunning = false;
            _cts?.Dispose();
            _cts = null;
            semaphore.Dispose();
        }
    }

    public void CancelJob()
    {
        try { _cts?.Cancel(); } catch { }
    }

    public async Task StartConversionJobAsync(IReadOnlyList<ConversionJobDescriptor> jobs, CancellationToken cancellationToken = default)
    {
        if (_isJobRunning)
        {
            return;
        }

        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _isJobRunning = true;

        // Pre-job: resolve output path collisions before any file is written.
        ResolveConversionCollisions(jobs);

        int parallelism = Math.Max(1, Environment.ProcessorCount - 2);
        var semaphore = new SemaphoreSlim(parallelism, parallelism);
        int completed = 0;
        var token = _cts.Token;

        try
        {
            var tasks = jobs.Select(async job =>
            {
                await semaphore.WaitAsync(token).ConfigureAwait(false);
                try
                {
                    if (token.IsCancellationRequested)
                    {
                        return;
                    }

                    Progress?.Invoke(this, new JobProgressState
                    {
                        CompletedCount = completed,
                        TotalCount = jobs.Count,
                        CurrentFileName = Path.GetFileName(job.InputPath),
                    });

                    await Task.Run(() =>
                    {
                        job.Result = job.Kind switch
                        {
                            ConversionKind.PdfToImage => _pdfTranscode.PdfToImage(
                                job.InputPath,
                                job.OutputPath,
                                job.TargetExtension,
                                job.QualityLevel,
                                job.PdfPageMode),
                            ConversionKind.ImageToPdf => _pdfTranscode.ImageToPdf(
                                job.InputPath,
                                job.OutputPath),
                            ConversionKind.ImagesToPdf => _pdfTranscode.ImagesToPdf(
                                job.CombinedInputPaths ?? new[] { job.InputPath },
                                job.OutputPath),
                            _ => _conversionService.Convert(
                                job.InputPath,
                                job.OutputPath,
                                job.TargetExtension,
                                job.QualityLevel,
                                job.StripMetadata,
                                job.TargetWidth,
                                job.TargetHeight),
                        };
                    }, token).ConfigureAwait(false);

                    Interlocked.Increment(ref completed);

                    Progress?.Invoke(this, new JobProgressState
                    {
                        CompletedCount = completed,
                        TotalCount = jobs.Count,
                        CurrentFileName = Path.GetFileName(job.InputPath),
                    });
                }
                finally
                {
                    semaphore.Release();
                }
            }).ToList();

            await Task.WhenAll(tasks).ConfigureAwait(false);
        }
        finally
        {
            _isJobRunning = false;
            _cts?.Dispose();
            _cts = null;
            semaphore.Dispose();
        }
    }

    // ── Collision resolution ─────────────────────────────────────────────────

    private static void ResolveCollisions(IReadOnlyList<CompressionJob> jobs)
    {
        // Keep a set of output paths committed so far in this batch.
        var committed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var job in jobs)
        {
            var path = job.OutputPath;
            if (!committed.Contains(path) && !File.Exists(path))
            {
                committed.Add(path);
                continue;
            }

            // Collision - append counter starting at 2.
            var dir = Path.GetDirectoryName(path) ?? string.Empty;
            var name = Path.GetFileNameWithoutExtension(path);
            var ext = Path.GetExtension(path);
            int counter = 2;
            string candidate;
            do
            {
                candidate = Path.Combine(dir, $"{name}_{counter}{ext}");
                counter++;
            } while (committed.Contains(candidate) || File.Exists(candidate));

            job.OutputPath = candidate;
            committed.Add(candidate);
        }
    }

    private static void ResolveConversionCollisions(IReadOnlyList<ConversionJobDescriptor> jobs)
    {
        var committed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var job in jobs)
        {
            var path = job.OutputPath;
            if (!committed.Contains(path) && !File.Exists(path))
            {
                committed.Add(path);
                continue;
            }

            var dir = Path.GetDirectoryName(path) ?? string.Empty;
            var name = Path.GetFileNameWithoutExtension(path);
            var ext = Path.GetExtension(path);
            int counter = 2;
            string candidate;
            do
            {
                candidate = Path.Combine(dir, $"{name}_{counter}{ext}");
                counter++;
            } while (committed.Contains(candidate) || File.Exists(candidate));

            job.OutputPath = candidate;
            committed.Add(candidate);
        }
    }
}
