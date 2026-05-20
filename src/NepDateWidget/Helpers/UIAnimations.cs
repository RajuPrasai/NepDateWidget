using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using NepDateWidget.ViewModels;

namespace NepDateWidget.Helpers;

/// <summary>
/// Subtle scale animations for any clickable element (ButtonBase, TabItem, MenuItem) that
/// contains a tabler icon (Path or Image in its visual tree). Animates the icon element
/// itself, not the container. Gated on MainViewModel.AnimationEnabled.
/// Register once at startup.
/// </summary>
internal static class UIAnimations
{
    private static MainViewModel? _vm;

    private const double IconHover = 1.06;
    private const double IconPress = 0.92;
    private const double Rest      = 1.00;

    private const int DurEnter   = 120;
    private const int DurLeave   = 100;
    private const int DurPress   = 70;
    private const int DurRelease = 100;

    /// <summary>
    /// Registers class-level routed event handlers. Call once in App.OnStartup after MainViewModel is created.
    /// </summary>
    public static void Register(MainViewModel vm)
    {
        _vm = vm;

        foreach (var type in new[] { typeof(ButtonBase), typeof(TabItem), typeof(MenuItem) })
        {
            EventManager.RegisterClassHandler(type, UIElement.MouseEnterEvent,
                new MouseEventHandler(OnEnter), handledEventsToo: true);
            EventManager.RegisterClassHandler(type, UIElement.MouseLeaveEvent,
                new MouseEventHandler(OnLeave), handledEventsToo: true);
            EventManager.RegisterClassHandler(type, UIElement.PreviewMouseLeftButtonDownEvent,
                new MouseButtonEventHandler(OnDown), handledEventsToo: true);
            EventManager.RegisterClassHandler(type, UIElement.MouseLeftButtonUpEvent,
                new MouseButtonEventHandler(OnUp), handledEventsToo: true);
        }
    }

    private static bool Enabled => _vm?.AnimationEnabled ?? true;

    private static void OnEnter(object sender, MouseEventArgs e)
    {
        if (!Enabled || sender is not FrameworkElement fe || !fe.IsEnabled) return;
        var icon = FindIcon(fe);
        if (icon != null) ScaleTo(icon, IconHover, DurEnter);
    }

    private static void OnLeave(object sender, MouseEventArgs e)
    {
        if (sender is not FrameworkElement fe) return;
        var icon = FindIcon(fe);
        if (icon != null) ScaleTo(icon, Rest, Enabled ? DurLeave : 0);
    }

    private static void OnDown(object sender, MouseButtonEventArgs e)
    {
        if (!Enabled || sender is not FrameworkElement fe || !fe.IsEnabled) return;
        var icon = FindIcon(fe);
        if (icon != null) ScaleTo(icon, IconPress, DurPress);
    }

    private static void OnUp(object sender, MouseButtonEventArgs e)
    {
        if (sender is not FrameworkElement fe) return;
        var icon = FindIcon(fe);
        if (icon != null) ScaleTo(icon, fe.IsMouseOver && Enabled ? IconHover : Rest, Enabled ? DurRelease : 0);
    }

    // ── Reset ────────────────────────────────────────────────────────────────

    /// <summary>
    /// Snaps all scale transforms back to 1.0 without animation.
    /// Call when AnimationEnabled changes to false.
    /// </summary>
    public static void ResetAllScales()
    {
        // Defensive: In test or headless contexts, Application.Current or Windows may be null or throw.
        try
        {
            var app = Application.Current;
            if (app == null || app.Windows == null)
                return;
            foreach (Window w in app.Windows)
                ResetInTree(w);
        }
        catch
        {
            // Swallow all exceptions for test and headless safety.
        }
    }

    private static void ResetInTree(DependencyObject root)
    {
        if (root is FrameworkElement fe && fe.RenderTransform is ScaleTransform st
            && (st.ScaleX != Rest || st.ScaleY != Rest))
        {
            st.BeginAnimation(ScaleTransform.ScaleXProperty, null);
            st.BeginAnimation(ScaleTransform.ScaleYProperty, null);
            st.ScaleX = Rest;
            st.ScaleY = Rest;
        }
        int count = VisualTreeHelper.GetChildrenCount(root);
        for (int i = 0; i < count; i++)
            ResetInTree(VisualTreeHelper.GetChild(root, i));
    }

    // ── Icon detection ───────────────────────────────────────────────────────

    // Returns the first Path or Image found in the visual tree, depth-limited to 8.
    // Text-only elements (no Path anywhere) naturally return null — no animation.
    private static FrameworkElement? FindIcon(FrameworkElement host) =>
        ScanForIcon(host, 0);

    private static FrameworkElement? ScanForIcon(DependencyObject node, int depth)
    {
        if (depth > 8) return null;
        int count = VisualTreeHelper.GetChildrenCount(node);
        for (int i = 0; i < count; i++)
        {
            var child = VisualTreeHelper.GetChild(node, i);
            if (child is Path or Image)
                return (FrameworkElement)child;
            var found = ScanForIcon(child, depth + 1);
            if (found != null) return found;
        }
        return null;
    }

    // ── Scale ────────────────────────────────────────────────────────────────

    private static void ScaleTo(FrameworkElement el, double target, int ms)
    {
        if (el.RenderTransform is not ScaleTransform)
        {
            el.RenderTransformOrigin = new Point(0.5, 0.5);
            el.RenderTransform = new ScaleTransform(Rest, Rest);
        }
        var st = (ScaleTransform)el.RenderTransform;

        if (ms == 0)
        {
            st.BeginAnimation(ScaleTransform.ScaleXProperty, null);
            st.BeginAnimation(ScaleTransform.ScaleYProperty, null);
            st.ScaleX = target;
            st.ScaleY = target;
            return;
        }

        var dur  = new Duration(TimeSpan.FromMilliseconds(ms));
        var ease = new QuadraticEase { EasingMode = EasingMode.EaseOut };
        st.BeginAnimation(ScaleTransform.ScaleXProperty, new DoubleAnimation(target, dur) { EasingFunction = ease });
        st.BeginAnimation(ScaleTransform.ScaleYProperty, new DoubleAnimation(target, dur) { EasingFunction = ease });
    }
}

