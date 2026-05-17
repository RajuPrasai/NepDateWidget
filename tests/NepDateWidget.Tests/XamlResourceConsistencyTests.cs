using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using NepDateWidget.ViewModels;

namespace NepDateWidget.Tests;

/// <summary>
/// Static analysis tests that parse XAML source files to catch two classes of runtime crash
/// before they reach the user:
///
/// 1. StaticResource key not found (XamlParseException at InitializeComponent time).
///    Root rule: {StaticResource K} in a UserControl XAML file resolves only against the
///    file's own resources and Application.Resources (App.xaml merged chain). Parent window
///    resources are NOT accessible at InitializeComponent time.
///
/// 2. TwoWay binding on a private-setter property (InvalidOperationException at layout time).
///    Root rule: ProgressBar.Value defaults to TwoWay. If the bound property has a private
///    setter, WPF throws when it tries to push a value back. Mode=OneWay is required.
/// </summary>
public sealed class XamlResourceConsistencyTests
{
    // ── Regex patterns ────────────────────────────────────────────────────────

    private static readonly Regex KeyDefinitionPattern =
        new(@"x:Key=""([^""]+)""", RegexOptions.Compiled);

    // Matches {StaticResource Name} - simple identifier keys only.
    // Excludes {StaticResource {x:Type ...}} (nested braces) which are type-keyed.
    private static readonly Regex StaticResourceRefPattern =
        new(@"\{StaticResource\s+([\w.]+)\}", RegexOptions.Compiled);

    // Matches Source="relative/path.xaml" - local resource file references only.
    private static readonly Regex MergedSourcePattern =
        new(@"Source=""((?!pack://)[^""]+\.xaml)""", RegexOptions.Compiled);

    // Matches <ProgressBar ... Value="{Binding ...}" - the full attribute value captured.
    // Uses Singleline so the element may span multiple lines.
    private static readonly Regex ProgressBarValueBinding =
        new(@"<ProgressBar\b[^>]*\bValue=""(\{Binding[^""]+\})""",
            RegexOptions.Compiled | RegexOptions.Singleline);

    // Matches selector TwoWay-default attributes with a binding value precisely.
    // Groups: (1) attribute name, (2) property name from binding.
    // Uses the exact attribute="..." form so it cannot misfire on
    // <DataTrigger Binding="{Binding X}" Value="True"> lines.
    private static readonly Regex SelectorAttributeBinding =
        new(@"\b(SelectedItem|SelectedIndex|SelectedValue)\s*=\s*""\{Binding\s+([\w.]+)(?:,[^}]*)?\}""",
            RegexOptions.Compiled);

    // Matches <Slider ... Value="{Binding PropertyName[,opts]}" ...>.
    // Groups: (1) full binding expression, (2) property name.
    // Slider inherits RangeBase.Value (BindsTwoWayByDefault). If bound to a
    // private-setter property the crash is identical to the ProgressBar case.
    private static readonly Regex SliderValueBinding =
        new(@"<Slider\b[^>]*\bValue=""(\{Binding\s+([\w.]+)(?:,[^}]*)?\})""",
            RegexOptions.Compiled | RegexOptions.Singleline);

    // Fast check: does this binding expression carry an explicit Mode=OneWay?
    private static readonly Regex ExplicitOneWay =
        new(@"Mode\s*=\s*OneWay(?!\s*ToSource)", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    // ── Source tree location ──────────────────────────────────────────────────

    private static string SourceRoot { get; } = LocateSourceRoot();

    private static string LocateSourceRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "NepDateWidget.slnx")))
            {
                return Path.Combine(dir.FullName, "src", "NepDateWidget");
            }

            dir = dir.Parent;
        }
        throw new InvalidOperationException(
            $"Cannot locate repository root from '{AppContext.BaseDirectory}'. " +
            "Expected to find NepDateWidget.slnx in an ancestor directory.");
    }

    // ── Key collection helpers ────────────────────────────────────────────────

    private static HashSet<string> CollectKeysRecursive(string filePath, HashSet<string>? visited = null)
    {
        visited ??= new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (!visited.Add(filePath))
        {
            return new HashSet<string>(StringComparer.Ordinal);
        }

        var keys = new HashSet<string>(StringComparer.Ordinal);
        if (!File.Exists(filePath))
        {
            return keys;
        }

        var content = File.ReadAllText(filePath);
        foreach (Match m in KeyDefinitionPattern.Matches(content))
        {
            keys.Add(m.Groups[1].Value);
        }

        var baseDir = Path.GetDirectoryName(filePath) ?? string.Empty;
        foreach (Match m in MergedSourcePattern.Matches(content))
        {
            var relative = m.Groups[1].Value.Replace('/', Path.DirectorySeparatorChar);
            var resolved = Path.GetFullPath(Path.Combine(baseDir, relative));
            if (File.Exists(resolved))
            {
                keys.UnionWith(CollectKeysRecursive(resolved, visited));
            }
        }

        return keys;
    }

    private static HashSet<string> BuildApplicationKeys()
        => CollectKeysRecursive(Path.Combine(SourceRoot, "App.xaml"));

    // ── Reflection helper ─────────────────────────────────────────────────────

    /// <summary>
    /// Returns all public property names that have a non-public setter across all
    /// ViewModel types in the main assembly.
    /// </summary>
    private static HashSet<string> GetViewModelPrivateSetterPropertyNames()
    {
        var vmAssembly = typeof(ViewModelBase).Assembly;
        var names      = new HashSet<string>(StringComparer.Ordinal);

        foreach (var type in vmAssembly.GetTypes())
        {
            if (type.Namespace?.StartsWith("NepDateWidget.ViewModels", StringComparison.Ordinal) != true)
            {
                continue;
            }

            if (!type.IsClass || type.IsAbstract)
            {
                continue;
            }

            foreach (var prop in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                var setter = prop.GetSetMethod(nonPublic: true);
                if (setter is not null && !setter.IsPublic)
                {
                    names.Add(prop.Name);
                }
            }
        }
        return names;
    }

    // ── StaticResource tests ──────────────────────────────────────────────────

    [Fact]
    public void Views_StaticResourceKeys_AreDefinedLocallyOrInApplicationResources()
    {
        var viewsDir = Path.Combine(SourceRoot, "Views");
        Assert.True(Directory.Exists(viewsDir), $"Views directory not found: {viewsDir}");

        var appKeys = BuildApplicationKeys();
        Assert.NotEmpty(appKeys);

        var violations = new List<string>();

        foreach (var xamlFile in Directory.GetFiles(viewsDir, "*.xaml", SearchOption.AllDirectories))
        {
            var content       = File.ReadAllText(xamlFile);
            var fileName      = Path.GetFileName(xamlFile);
            var localKeys     = CollectKeysRecursive(xamlFile);
            var availableKeys = new HashSet<string>(localKeys.Union(appKeys), StringComparer.Ordinal);

            foreach (Match m in StaticResourceRefPattern.Matches(content))
            {
                var key = m.Groups[1].Value;
                if (!availableKeys.Contains(key))
                {
                    violations.Add($"  {fileName}: {{{{StaticResource {key}}}}} - key not found in local resources or Application.Resources");
                }
            }
        }

        Assert.True(violations.Count == 0,
            "StaticResource keys not resolvable at InitializeComponent time:\n" +
            string.Join("\n", violations));
    }

    [Fact]
    public void ResourceDictionaries_StaticResourceKeys_AreDefinedWithinScope()
    {
        var resourcesDir = Path.Combine(SourceRoot, "Resources");
        if (!Directory.Exists(resourcesDir))
        {
            return;
        }

        var appKeys    = BuildApplicationKeys();
        var violations = new List<string>();

        foreach (var xamlFile in Directory.GetFiles(resourcesDir, "*.xaml", SearchOption.AllDirectories))
        {
            var content       = File.ReadAllText(xamlFile);
            var fileName      = Path.GetRelativePath(SourceRoot, xamlFile);
            var localKeys     = CollectKeysRecursive(xamlFile);
            var availableKeys = new HashSet<string>(localKeys.Union(appKeys), StringComparer.Ordinal);

            foreach (Match m in StaticResourceRefPattern.Matches(content))
            {
                var key = m.Groups[1].Value;
                if (!availableKeys.Contains(key))
                {
                    violations.Add($"  {fileName}: {{{{StaticResource {key}}}}} - key not resolvable");
                }
            }
        }

        Assert.True(violations.Count == 0,
            "StaticResource keys not resolvable in resource dictionaries:\n" +
            string.Join("\n", violations));
    }

    [Fact]
    public void AppXaml_MergedDictionaries_AllFilesExist()
    {
        var appXaml = Path.Combine(SourceRoot, "App.xaml");
        Assert.True(File.Exists(appXaml), $"App.xaml not found at: {appXaml}");

        var content = File.ReadAllText(appXaml);
        var baseDir = Path.GetDirectoryName(appXaml)!;
        var missing = new List<string>();

        foreach (Match m in MergedSourcePattern.Matches(content))
        {
            var relative = m.Groups[1].Value.Replace('/', Path.DirectorySeparatorChar);
            var full     = Path.GetFullPath(Path.Combine(baseDir, relative));
            if (!File.Exists(full))
            {
                missing.Add($"  {m.Groups[1].Value} → {full}");
            }
        }

        Assert.True(missing.Count == 0,
            "App.xaml references missing ResourceDictionary files:\n" +
            string.Join("\n", missing));
    }

    // ── TwoWay-default binding safety tests ───────────────────────────────────

    [Fact]
    public void ProgressBar_ValueBinding_AlwaysUsesOneWayMode()
    {
        // ProgressBar is display-only. RangeBase.Value defaults to TwoWay.
        // If the bound property has a private setter, WPF throws at layout time:
        //   "A TwoWay or OneWayToSource binding cannot work on the read-only property
        //    'CompletedCount' of type 'CompressionViewModel'."
        // Convention: ALL ProgressBar.Value bindings must carry Mode=OneWay.
        var viewsDir = Path.Combine(SourceRoot, "Views");
        Assert.True(Directory.Exists(viewsDir));

        var violations = new List<string>();

        foreach (var xamlFile in Directory.GetFiles(viewsDir, "*.xaml", SearchOption.AllDirectories))
        {
            var content  = File.ReadAllText(xamlFile);
            var fileName = Path.GetFileName(xamlFile);

            foreach (Match m in ProgressBarValueBinding.Matches(content))
            {
                var attrValue = m.Groups[1].Value;
                if (!ExplicitOneWay.IsMatch(attrValue))
                {
                    violations.Add($"  {fileName}: ProgressBar Value=\"{attrValue}\" - missing Mode=OneWay");
                }
            }
        }

        Assert.True(violations.Count == 0,
            "ProgressBar.Value bindings must include Mode=OneWay (default is TwoWay, " +
            "crashes if the target property has a private setter):\n" +
            string.Join("\n", violations));
    }

    [Fact]
    public void SelectorControls_BoundToPrivateSetterProperties_MustUseOneWayMode()
    {
        // ComboBox/ListBox SelectedItem, SelectedIndex, SelectedValue default to TwoWay.
        // If the bound ViewModel property has a private setter, WPF throws at layout time.
        //
        // The regex matches AttributeName="{Binding PropertyName}" precisely so it cannot
        // misfire on DataTrigger Value="True" lines where a {Binding ...} appears in a
        // different attribute on the same element.
        var viewsDir       = Path.Combine(SourceRoot, "Views");
        var privateSetters = GetViewModelPrivateSetterPropertyNames();
        Assert.NotEmpty(privateSetters);

        var violations = new List<string>();

        foreach (var xamlFile in Directory.GetFiles(viewsDir, "*.xaml", SearchOption.AllDirectories))
        {
            var content  = File.ReadAllText(xamlFile);
            var fileName = Path.GetFileName(xamlFile);

            foreach (Match m in SelectorAttributeBinding.Matches(content))
            {
                var attrName = m.Groups[1].Value;
                var propName = m.Groups[2].Value;
                if (!privateSetters.Contains(propName))
                {
                    continue;
                }

                if (!ExplicitOneWay.IsMatch(m.Value))
                {
                    violations.Add($"  {fileName}: {attrName}=\"{{Binding {propName}}}\" - " +
                                   $"property has a non-public setter, binding is missing Mode=OneWay");
                }
            }
        }

        Assert.True(violations.Count == 0,
            "Selector attribute bindings to private-setter ViewModel properties " +
            "will throw at layout time:\n" +
            string.Join("\n", violations));
    }

    [Fact]
    public void SliderValue_BoundToPrivateSetterProperties_MustUseOneWayMode()
    {
        // Slider inherits RangeBase.Value which has BindsTwoWayByDefault.
        // If the bound property has a private setter, WPF throws the same
        // InvalidOperationException as with ProgressBar at layout time.
        // Unlike ProgressBar (always display-only), Slider TwoWay is correct when
        // the property has a public setter (user interaction). This test only flags
        // bindings to private-setter properties that are missing Mode=OneWay.
        var viewsDir       = Path.Combine(SourceRoot, "Views");
        var privateSetters = GetViewModelPrivateSetterPropertyNames();
        Assert.NotEmpty(privateSetters);

        var violations = new List<string>();

        foreach (var xamlFile in Directory.GetFiles(viewsDir, "*.xaml", SearchOption.AllDirectories))
        {
            var content  = File.ReadAllText(xamlFile);
            var fileName = Path.GetFileName(xamlFile);

            foreach (Match m in SliderValueBinding.Matches(content))
            {
                var bindingExpr = m.Groups[1].Value;
                var propName    = m.Groups[2].Value;
                if (!privateSetters.Contains(propName))
                {
                    continue;
                }

                if (!ExplicitOneWay.IsMatch(bindingExpr))
                {
                    violations.Add($"  {fileName}: Slider Value=\"{bindingExpr}\" - " +
                                   $"property '{propName}' has a non-public setter, binding is missing Mode=OneWay");
                }
            }
        }

        Assert.True(violations.Count == 0,
            "Slider.Value bindings to private-setter ViewModel properties " +
            "will throw at layout time:\n" +
            string.Join("\n", violations));
    }

    [Fact]
    public void MoreView_SubViews_UseRootScopedVisibilityBindings()
    {
        // Compression/Resize sub-views have their own DataContext.
        // Visibility bindings must explicitly source back to MoreView's DataContext,
        // otherwise binding falls back and sub-views render on the home grid.
        var moreViewPath = Path.Combine(SourceRoot, "Views", "MoreView.xaml");
        Assert.True(File.Exists(moreViewPath));

        var content = File.ReadAllText(moreViewPath);

        Assert.Contains("x:Name=\"RootMoreView\"", content, StringComparison.Ordinal);
        Assert.Contains("DataContext.IsSubViewCompression", content, StringComparison.Ordinal);
        Assert.Contains("DataContext.IsSubViewResize", content, StringComparison.Ordinal);
        Assert.Contains("ElementName=RootMoreView", content, StringComparison.Ordinal);
        Assert.Contains("FallbackValue=Collapsed", content, StringComparison.Ordinal);
    }

    [Fact]
    public void CompressionAndResize_Sliders_UseWidgetSliderStyle()
    {
        // Enforce visual consistency with the rest of the widget by using the
        // shared iOS-style slider template from theme resources.
        var compressionViewPath = Path.Combine(SourceRoot, "Views", "CompressionView.xaml");
        var resizeViewPath      = Path.Combine(SourceRoot, "Views", "ResizeView.xaml");

        Assert.True(File.Exists(compressionViewPath));
        Assert.True(File.Exists(resizeViewPath));

        var compressionXaml = File.ReadAllText(compressionViewPath);
        var resizeXaml      = File.ReadAllText(resizeViewPath);

        Assert.Contains("Value=\"{Binding CompressionLevel}\"", compressionXaml, StringComparison.Ordinal);
        Assert.Contains("Style=\"{DynamicResource IosSlider}\"", compressionXaml, StringComparison.Ordinal);

        Assert.Contains("Value=\"{Binding QualityLevel}\"", resizeXaml, StringComparison.Ordinal);
        Assert.Contains("Style=\"{DynamicResource IosSlider}\"", resizeXaml, StringComparison.Ordinal);
    }

    [Fact]
    public void CompressionAndResize_Sliders_UseFivePointScale()
    {
        var compressionViewPath = Path.Combine(SourceRoot, "Views", "CompressionView.xaml");
        var resizeViewPath      = Path.Combine(SourceRoot, "Views", "ResizeView.xaml");

        Assert.True(File.Exists(compressionViewPath));
        Assert.True(File.Exists(resizeViewPath));

        var compressionXaml = File.ReadAllText(compressionViewPath);
        var resizeXaml      = File.ReadAllText(resizeViewPath);

        Assert.Contains("Value=\"{Binding CompressionLevel}\"", compressionXaml, StringComparison.Ordinal);
        Assert.Contains("Maximum=\"4\"", compressionXaml, StringComparison.Ordinal);

        Assert.Contains("Value=\"{Binding QualityLevel}\"", resizeXaml, StringComparison.Ordinal);
        Assert.Contains("Maximum=\"4\"", resizeXaml, StringComparison.Ordinal);
    }

    [Fact]
    public void CompressionView_AdvancedPanel_ContainsResizeInputs()
    {
        var compressionViewPath = Path.Combine(SourceRoot, "Views", "CompressionView.xaml");
        Assert.True(File.Exists(compressionViewPath));

        var compressionXaml = File.ReadAllText(compressionViewPath);

        Assert.Contains("{Binding AdvOptionalResizeLabel}", compressionXaml, StringComparison.Ordinal);
        Assert.Contains("{Binding ResizeWidthText", compressionXaml, StringComparison.Ordinal);
        Assert.Contains("{Binding ResizeHeightText", compressionXaml, StringComparison.Ordinal);
    }

    [Fact]
    public void CompressionAndResize_SummaryBanners_DoNotRequireManualNewJobButton()
    {
        var compressionViewPath = Path.Combine(SourceRoot, "Views", "CompressionView.xaml");
        var resizeViewPath      = Path.Combine(SourceRoot, "Views", "ResizeView.xaml");

        Assert.True(File.Exists(compressionViewPath));
        Assert.True(File.Exists(resizeViewPath));

        var compressionXaml = File.ReadAllText(compressionViewPath);
        var resizeXaml      = File.ReadAllText(resizeViewPath);

        Assert.DoesNotContain("StartNewJobCommand", compressionXaml, StringComparison.Ordinal);
        Assert.DoesNotContain("StartNewJobCommand", resizeXaml, StringComparison.Ordinal);
    }
}
