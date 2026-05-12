using NepDateWidget.Helpers;
using NepDateWidget.Services;
using System.Diagnostics;
using System.Reflection;
using System.Windows.Input;

namespace NepDateWidget.ViewModels;

/// <summary>
/// View model for the About tab.
/// Exposes localized labels, the app version, and link-open commands.
/// No mutable state beyond labels - all bindings are one-way.
/// </summary>
public sealed class AboutViewModel : ViewModelBase
{
    private readonly ILocalizationService _loc;

    // ─────────────────────────────────────────────────────────────────────────
    // Commands
    // ─────────────────────────────────────────────────────────────────────────

    public ICommand OpenSupportCommand    { get; }
    public ICommand OpenRepoCommand       { get; }
    public ICommand OpenBugReportCommand  { get; }
    public ICommand OpenChangelogCommand  { get; }
    public ICommand OpenWebsiteCommand    { get; }

    // ─────────────────────────────────────────────────────────────────────────
    // Static / version info
    // ─────────────────────────────────────────────────────────────────────────

    public string AppVersion { get; } =
        Assembly.GetExecutingAssembly()
                .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
                ?.InformationalVersion
                ?.Split('+')[0]   // strip git hash suffix if present
        ?? Assembly.GetExecutingAssembly().GetName().Version?.ToString(3)
        ?? "1.0.0";

    // ─────────────────────────────────────────────────────────────────────────
    // Localized labels
    // ─────────────────────────────────────────────────────────────────────────

    public string TaglineLabel       { get; private set; } = string.Empty;
    public string SupportHeading     { get; private set; } = string.Empty;
    public string SupportBody        { get; private set; } = string.Empty;
    public string SupportButtonLabel { get; private set; } = string.Empty;
    public string LinksHeading       { get; private set; } = string.Empty;
    public string RepoButtonLabel    { get; private set; } = string.Empty;
    public string BugButtonLabel     { get; private set; } = string.Empty;
    public string ChangelogButtonLabel { get; private set; } = string.Empty;
    public string WebsiteButtonLabel { get; private set; } = string.Empty;
    public string BuiltByLabel       { get; private set; } = string.Empty;

    // ─────────────────────────────────────────────────────────────────────────
    // Construction
    // ─────────────────────────────────────────────────────────────────────────

    public AboutViewModel(ILocalizationService localizationService)
    {
        _loc = localizationService ?? throw new ArgumentNullException(nameof(localizationService));

        OpenSupportCommand   = new RelayCommand(() => OpenUrl("https://buymemomo.com/rajuprasai"));
        OpenRepoCommand      = new RelayCommand(() => OpenUrl("https://github.com/RajuPrasai/NepDateWidget"));
        OpenBugReportCommand = new RelayCommand(() => OpenUrl("https://github.com/RajuPrasai/NepDateWidget/issues/new"));
        OpenChangelogCommand = new RelayCommand(() => OpenUrl("https://rajuprasai.github.io/NepDateWidget/changelog.html"));
        OpenWebsiteCommand   = new RelayCommand(() => OpenUrl("https://rajuprasai.github.io/NepDateWidget/"));

        RefreshLabels();
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Language change
    // ─────────────────────────────────────────────────────────────────────────

    public void OnLanguageChanged() => RefreshLabels();

    private void RefreshLabels()
    {
        TaglineLabel         = _loc.Get("about.tagline");
        SupportHeading       = _loc.Get("about.support_heading");
        SupportBody          = _loc.Get("about.support_body");
        SupportButtonLabel   = _loc.Get("about.support_button");
        LinksHeading         = _loc.Get("about.links_heading");
        RepoButtonLabel      = _loc.Get("about.repo_button");
        BugButtonLabel       = _loc.Get("about.bug_button");
        ChangelogButtonLabel = _loc.Get("about.changelog_button");
        WebsiteButtonLabel   = _loc.Get("about.website_button");
        BuiltByLabel         = _loc.Get("about.built_by");

        OnPropertyChanged(nameof(TaglineLabel));
        OnPropertyChanged(nameof(SupportHeading));
        OnPropertyChanged(nameof(SupportBody));
        OnPropertyChanged(nameof(SupportButtonLabel));
        OnPropertyChanged(nameof(LinksHeading));
        OnPropertyChanged(nameof(RepoButtonLabel));
        OnPropertyChanged(nameof(BugButtonLabel));
        OnPropertyChanged(nameof(ChangelogButtonLabel));
        OnPropertyChanged(nameof(WebsiteButtonLabel));
        OnPropertyChanged(nameof(BuiltByLabel));
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Helpers
    // ─────────────────────────────────────────────────────────────────────────

    private static void OpenUrl(string url)
    {
        try
        {
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }
        catch { /* non-fatal - user can copy the URL manually */ }
    }
}
