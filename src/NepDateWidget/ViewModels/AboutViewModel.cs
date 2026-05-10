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

    public ICommand OpenSupportCommand { get; }
    public ICommand OpenRepoCommand    { get; }

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

    public string VersionLabel       { get; private set; } = string.Empty;
    public string TaglineLabel       { get; private set; } = string.Empty;
    public string FeaturesHeading    { get; private set; } = string.Empty;
    public string FeatureCalendar    { get; private set; } = string.Empty;
    public string FeatureConverter   { get; private set; } = string.Empty;
    public string FeatureBanking     { get; private set; } = string.Empty;
    public string FeatureText        { get; private set; } = string.Empty;
    public string FeatureNetwork     { get; private set; } = string.Empty;
    public string FeatureReminders   { get; private set; } = string.Empty;
    public string FeatureThemes      { get; private set; } = string.Empty;
    public string SupportHeading     { get; private set; } = string.Empty;
    public string SupportBody        { get; private set; } = string.Empty;
    public string SupportButtonLabel { get; private set; } = string.Empty;
    public string LinksHeading       { get; private set; } = string.Empty;
    public string RepoButtonLabel    { get; private set; } = string.Empty;
    public string BuiltByLabel       { get; private set; } = string.Empty;

    // ─────────────────────────────────────────────────────────────────────────
    // Construction
    // ─────────────────────────────────────────────────────────────────────────

    public AboutViewModel(ILocalizationService localizationService)
    {
        _loc = localizationService ?? throw new ArgumentNullException(nameof(localizationService));

        OpenSupportCommand = new RelayCommand(() => OpenUrl("https://buymemomo.com/rajuprasai"));
        OpenRepoCommand    = new RelayCommand(() => OpenUrl("https://github.com/RajuPrasai/NepDateWidget"));

        RefreshLabels();
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Language change
    // ─────────────────────────────────────────────────────────────────────────

    public void OnLanguageChanged() => RefreshLabels();

    private void RefreshLabels()
    {
        VersionLabel       = _loc.Get("about.version_label");
        TaglineLabel       = _loc.Get("about.tagline");
        FeaturesHeading    = _loc.Get("about.features_heading");
        FeatureCalendar    = _loc.Get("about.feature_calendar");
        FeatureConverter   = _loc.Get("about.feature_converter");
        FeatureBanking     = _loc.Get("about.feature_banking");
        FeatureText        = _loc.Get("about.feature_text");
        FeatureNetwork     = _loc.Get("about.feature_network");
        FeatureReminders   = _loc.Get("about.feature_reminders");
        FeatureThemes      = _loc.Get("about.feature_themes");
        SupportHeading     = _loc.Get("about.support_heading");
        SupportBody        = _loc.Get("about.support_body");
        SupportButtonLabel = _loc.Get("about.support_button");
        LinksHeading       = _loc.Get("about.links_heading");
        RepoButtonLabel    = _loc.Get("about.repo_button");
        BuiltByLabel       = _loc.Get("about.built_by");

        OnPropertyChanged(nameof(VersionLabel));
        OnPropertyChanged(nameof(TaglineLabel));
        OnPropertyChanged(nameof(FeaturesHeading));
        OnPropertyChanged(nameof(FeatureCalendar));
        OnPropertyChanged(nameof(FeatureConverter));
        OnPropertyChanged(nameof(FeatureBanking));
        OnPropertyChanged(nameof(FeatureText));
        OnPropertyChanged(nameof(FeatureNetwork));
        OnPropertyChanged(nameof(FeatureReminders));
        OnPropertyChanged(nameof(FeatureThemes));
        OnPropertyChanged(nameof(SupportHeading));
        OnPropertyChanged(nameof(SupportBody));
        OnPropertyChanged(nameof(SupportButtonLabel));
        OnPropertyChanged(nameof(LinksHeading));
        OnPropertyChanged(nameof(RepoButtonLabel));
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
