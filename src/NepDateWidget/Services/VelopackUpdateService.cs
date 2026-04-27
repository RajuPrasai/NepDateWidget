using NepDateWidget.Helpers;
using System.IO;
using System.Reflection;
using Velopack;
using Velopack.Sources;

namespace NepDateWidget.Services;

/// <summary>
/// Velopack-backed update service.
///
/// Feed resolution (in priority order):
///   1. <c>NEPDATE_UPDATE_FEED</c> environment variable. Accepted forms:
///        - Local folder path        e.g. <c>D:\Personal\NepDateWidget\releases</c>
///        - <c>file://</c> URL       e.g. <c>file:///D:/Personal/NepDateWidget/releases</c>
///        - GitHub repo URL          e.g. <c>https://github.com/owner/repo</c>
///      Folder/file:// paths use <see cref="SimpleFileSource"/> for offline testing.
///      Any other URL is treated as a GitHub repo.
///   2. <see cref="DefaultGitHubFeed"/> constant below.
/// </summary>
public sealed class VelopackUpdateService : IUpdateService
{
    // Default release feed. Override at runtime via NEPDATE_UPDATE_FEED env var.
    public const string DefaultGitHubFeed = "https://github.com/RajuPrasai/NepDateWidget";

    private readonly UpdateManager? _manager;
    private readonly string _currentVersion;

    public VelopackUpdateService()
    {
        _currentVersion = Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "0.0.0";

        try
        {
            var feed = Environment.GetEnvironmentVariable("NEPDATE_UPDATE_FEED");
            if (string.IsNullOrWhiteSpace(feed))
                feed = DefaultGitHubFeed;

            // Reject plain-HTTP override values. Local paths and file:// URLs
            // remain allowed for offline testing; everything else must use TLS
            // so a misconfigured machine cannot accept tampered packages.
            if (feed.StartsWith("http://", StringComparison.OrdinalIgnoreCase))
            {
                Log.Warn($"Ignoring insecure (http://) update feed override; falling back to default. Value was: {feed}");
                feed = DefaultGitHubFeed;
            }

            var source = BuildSource(feed);

            // Only initialise when running from a Velopack install. Local dev
            // builds will throw inside UpdateManager construction; we swallow
            // and stay disabled so tests and `dotnet run` are unaffected.
            _manager = new UpdateManager(source);
        }
        catch (Exception ex)
        {
            Log.Info($"Update manager unavailable (likely dev build): {ex.Message}");
            _manager = null;
        }
    }

    public bool IsInstalled => _manager?.IsInstalled ?? false;
    public string CurrentVersion => _currentVersion;

    public async Task<UpdateCheckResult> CheckAsync(CancellationToken ct = default)
    {
        if (_manager is null || !_manager.IsInstalled)
            return new UpdateCheckResult(false, null, _currentVersion, "Updates are not enabled in this build (dev or portable).");

        try
        {
            var info = await _manager.CheckForUpdatesAsync().ConfigureAwait(false);
            if (info is null)
                return new UpdateCheckResult(false, null, _currentVersion);

            return new UpdateCheckResult(true, info.TargetFullRelease?.Version?.ToString(), _currentVersion);
        }
        catch (Exception ex)
        {
            Log.Warn($"Update check failed: {ex.Message}");
            return new UpdateCheckResult(false, null, _currentVersion, ex.Message);
        }
    }

    public async Task<bool> DownloadAndApplyAsync(CancellationToken ct = default)
    {
        if (_manager is null || !_manager.IsInstalled) return false;

        try
        {
            var info = await _manager.CheckForUpdatesAsync().ConfigureAwait(false);
            if (info is null) return false;

            await _manager.DownloadUpdatesAsync(info).ConfigureAwait(false);
            // Schedules the upgrade on next process restart and exits the app.
            _manager.ApplyUpdatesAndRestart(info);
            return true;
        }
        catch (Exception ex)
        {
            Log.Error("Failed to download/apply update", ex);
            return false;
        }
    }

    /// <summary>
    /// Picks a Velopack source based on the feed string. A local folder or
    /// <c>file://</c> URL maps to <see cref="SimpleFileSource"/> for offline
    /// testing; everything else is treated as a GitHub repo.
    /// </summary>
    private static IUpdateSource BuildSource(string feed)
    {
        // file:// URL — convert to a local path and use SimpleFileSource.
        if (Uri.TryCreate(feed, UriKind.Absolute, out var uri) && uri.IsFile)
        {
            var dir = new DirectoryInfo(uri.LocalPath);
            Log.Info($"Update feed: local folder (file://) {dir.FullName}");
            return new SimpleFileSource(dir);
        }

        // Bare local path (drive-letter or UNC) — use SimpleFileSource.
        if (Path.IsPathRooted(feed) && !feed.StartsWith("http", StringComparison.OrdinalIgnoreCase))
        {
            var dir = new DirectoryInfo(feed);
            Log.Info($"Update feed: local folder {dir.FullName}");
            return new SimpleFileSource(dir);
        }

        // Anything else — assume GitHub repo URL.
        Log.Info($"Update feed: GitHub {feed}");
        return new GithubSource(feed, accessToken: null, prerelease: false);
    }
}
