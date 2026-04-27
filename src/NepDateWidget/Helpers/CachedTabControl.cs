using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;

namespace NepDateWidget.Helpers;

/// <summary>
/// TabControl that caches tab content to eliminate visual tree rebuild on tab switch.
/// Instantiates each tab's visual tree once and toggles Visibility instead of
/// destroying and recreating it, removing the visible flash on every tab change.
/// Template must contain a Panel named PART_ContentPanel.
/// </summary>
[TemplatePart(Name = ContentPanelName, Type = typeof(Panel))]
public sealed class CachedTabControl : TabControl
{
    private const string ContentPanelName = "PART_ContentPanel";
    private Panel? _contentPanel;

    public CachedTabControl()
    {
        Loaded += (_, _) => ShowSelectedTab();
    }

    public override void OnApplyTemplate()
    {
        base.OnApplyTemplate();
        _contentPanel = GetTemplateChild(ContentPanelName) as Panel;
        ShowSelectedTab();
    }

    protected override void OnSelectionChanged(SelectionChangedEventArgs e)
    {
        base.OnSelectionChanged(e);
        ShowSelectedTab();
    }

    private static readonly DoubleAnimation FadeIn = new(0.0, 1.0, TimeSpan.FromMilliseconds(180))
    {
        EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
    };

    private void ShowSelectedTab()
    {
        if (_contentPanel is null) return;

        // Hide all cached content presenters
        foreach (UIElement child in _contentPanel.Children)
            child.Visibility = Visibility.Collapsed;

        if (SelectedIndex < 0 || SelectedIndex >= Items.Count) return;
        if (Items[SelectedIndex] is not TabItem tabItem) return;

        // Show if already cached
        foreach (ContentPresenter cp in _contentPanel.Children)
        {
            if (ReferenceEquals(cp.Tag, tabItem))
            {
                cp.Visibility = Visibility.Visible;
                cp.BeginAnimation(OpacityProperty, FadeIn);
                return;
            }
        }

        // First visit: detach content from TabItem and host in our panel
        var content = tabItem.Content;
        tabItem.Content = null;

        var presenter = new ContentPresenter
        {
            Content = content,
            Tag = tabItem
        };
        _contentPanel.Children.Add(presenter);
        presenter.BeginAnimation(OpacityProperty, FadeIn);
    }
}
