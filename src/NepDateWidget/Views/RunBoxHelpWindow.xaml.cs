using NepDateWidget.Services;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Animation;

namespace NepDateWidget.Views;

public partial class RunBoxHelpWindow : Window
{
    private bool _isClosing;

    public RunBoxHelpWindow(ILocalizationService loc, IShortcutsService shortcuts, IScriptService? scripts)
    {
        InitializeComponent();

        TitleText.Text = loc.Get("help.runbox.title");
        DescText.Text = loc.Get("help.runbox.desc");
        KeysHeading.Text = loc.Get("help.runbox.keys.title");
        ShortcutsHeading.Text = loc.Get("help.runbox.shortcut.title");
        ScriptsHeading.Text = loc.Get("help.runbox.script.title");

        KeysList.ItemsSource = new[]
        {
            new KeyValuePair<string, string>("Enter",          loc.Get("help.runbox.key.enter")),
            new KeyValuePair<string, string>("Tab",            loc.Get("help.runbox.key.tab")),
            new KeyValuePair<string, string>("↑ / ↓",          loc.Get("help.runbox.key.arrows")),
            new KeyValuePair<string, string>("Esc",            loc.Get("help.runbox.key.esc")),
            new KeyValuePair<string, string>("= expr",         loc.Get("help.runbox.key.calc")),
            new KeyValuePair<string, string>("prefix Space",   loc.Get("help.runbox.key.prefix")),
            new KeyValuePair<string, string>("scr Space",      loc.Get("help.runbox.key.scr")),
        };

        var prefixItems = new List<KeyValuePair<string, string>>();
        foreach (var kvp in shortcuts.PrefixSiteNames)
        {
            prefixItems.Add(new KeyValuePair<string, string>(kvp.Key, kvp.Value));
        }

        ShortcutsList.ItemsSource = prefixItems;

        var scriptItems = scripts?.GetAll() ?? System.Array.Empty<NepDateWidget.Models.ScriptEntry>();
        ScriptsList.ItemsSource = scriptItems;

        if (scriptItems.Count == 0)
        {
            ScriptsHeading.Visibility = Visibility.Collapsed;
            ScriptsPanel.Visibility = Visibility.Collapsed;
        }

        MouseLeftButtonDown += (_, _) => { try { DragMove(); } catch { } };
        Deactivated += OnDeactivated;

        Loaded += (_, _) =>
        {
            var anim = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(150));
            BeginAnimation(OpacityProperty, anim);
        };
    }

    private void CloseBtn_Click(object sender, RoutedEventArgs e) => DoClose();

    private void OnDeactivated(object? sender, EventArgs e)
    {
        if (_isClosing)
        {
            return;
        }

        Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.ContextIdle, () =>
        {
            if (_isClosing)
            {
                return;
            }

            DoClose();
        });
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            DoClose();
            e.Handled = true;
        }
        base.OnKeyDown(e);
    }

    private void DoClose()
    {
        if (_isClosing)
        {
            return;
        }

        _isClosing = true;
        Deactivated -= OnDeactivated;
        try { Close(); } catch { }
    }
}
