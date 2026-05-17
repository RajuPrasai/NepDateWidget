using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;

namespace NepDateWidget.Helpers;

/// <summary>
/// TabControl that caches tab content to eliminate visual tree rebuild on tab switch.
/// Instantiates each tab's visual tree once and toggles Visibility instead of
/// destroying and recreating it, removing the visible flash on every tab change.
/// Template must contain a Panel named PART_ContentPanel.
/// All tab content is moved into PART_ContentPanel eagerly on template application so
/// every view is connected to the visual tree from startup. This is required for VS
/// XAML Hot Reload, which uses XamlDiagnostics to walk the visual tree — logical-tree-
/// only elements (content still in TabItem.Content) are invisible to the hot reload
/// engine and would report "applied to 0 elements".
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
        // Move all tab content into the panel immediately. Instances are already
        // created (they are XAML children of the host window); this only connects
        // them to the visual tree. Non-selected tabs remain Visibility.Collapsed so
        // there is no rendering or layout cost.
        foreach (TabItem tabItem in Items.OfType<TabItem>())
        {
            EnsureTabLoaded(tabItem);
        }

        ShowSelectedTab();
    }

    protected override void OnSelectionChanged(SelectionChangedEventArgs e)
    {
        base.OnSelectionChanged(e);
        ShowSelectedTab();
    }

    private static readonly DoubleAnimation FadeIn = new(0.0, 1.0, TimeSpan.FromMilliseconds(180))
    {
        EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut },
        FillBehavior = FillBehavior.Stop
    };

    private Border? EnsureTabLoaded(TabItem tabItem)
    {
        if (_contentPanel is null)
        {
            return null;
        }

        foreach (Border b in _contentPanel.Children.OfType<Border>())
        {
            if (ReferenceEquals(b.Tag, tabItem))
            {
                return b;
            }
        }

        var content = tabItem.Content as UIElement;
        tabItem.Content = null;

        var border = new Border
        {
            Child = content,
            Tag = tabItem,
            Visibility = Visibility.Collapsed,
        };
        _contentPanel.Children.Add(border);
        return border;
    }

    private void ShowSelectedTab()
    {
        if (_contentPanel is null)
        {
            return;
        }

        foreach (UIElement child in _contentPanel.Children)
        {
            child.Visibility = Visibility.Collapsed;
        }

        if (SelectedIndex < 0 || SelectedIndex >= Items.Count)
        {
            return;
        }

        if (Items[SelectedIndex] is not TabItem tabItem)
        {
            return;
        }

        var border = EnsureTabLoaded(tabItem);
        if (border is null)
        {
            return;
        }

        border.Visibility = Visibility.Visible;
        border.BeginAnimation(OpacityProperty, FadeIn);
    }
}
