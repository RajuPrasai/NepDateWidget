using NepDateWidget.Helpers;
using NepDateWidget.Services;
using NepDateWidget.Tests.Services;

namespace NepDateWidget.Tests.Helpers;

public sealed class DateFormatterTests
{
    private static LocalizationService Loc(string lang)
    {
        var l = new LocalizationService();
        l.SetLanguage(lang);
        return l;
    }

    [Fact]
    public void Build_English_ProducesFourOptions_InExpectedOrder()
    {
        var adapter = new FakeNepaliDateAdapter();   // 2082/12/20 -> 2026-04-03
        var loc     = Loc("en");

        var opts = DateFormatter.Build(2082, 12, 20, adapter, loc, isNepali: false);

        Assert.Equal(4, opts.Count);
        Assert.Equal(new[] { "bs_short", "bs_long", "ad_iso", "ad_long" },
                     opts.Select(o => o.Key).ToArray());
    }

    [Fact]
    public void Build_English_FormatsValuesCorrectly()
    {
        var adapter = new FakeNepaliDateAdapter();
        var loc     = Loc("en");

        var opts = DateFormatter.Build(2082, 12, 20, adapter, loc, isNepali: false);

        var byKey = opts.ToDictionary(o => o.Key);
        Assert.Equal("2082/12/20",                        byKey["bs_short"].Value);
        Assert.Equal("Chaitra 20, 2082",                  byKey["bs_long"].Value);
        Assert.Equal("2026-04-03",                        byKey["ad_iso"].Value);
        Assert.Equal("3 Apr 2026",                        byKey["ad_long"].Value);
    }

    [Fact]
    public void Build_Header_IsTheValueItself_NoLabelPrefix()
    {
        var adapter = new FakeNepaliDateAdapter();
        var loc     = Loc("en");

        var opts = DateFormatter.Build(2082, 12, 20, adapter, loc, isNepali: false);

        // Header == Value: the menu has a separate "Copy" header row, the
        // option rows show only the formatted date.
        foreach (var o in opts)
            Assert.Equal(o.Value, o.Header);
    }

    [Fact]
    public void Build_Nepali_UsesUnicodeForBs_ButKeepsAdAscii()
    {
        var adapter = new FakeNepaliDateAdapter();
        var loc     = Loc("ne");

        var opts = DateFormatter.Build(2082, 12, 20, adapter, loc, isNepali: true);

        var byKey = opts.ToDictionary(o => o.Key);
        // Nepali BS strings come from FormatBsShortNe / FormatBsLongNe (fakes append markers)
        Assert.Contains("unicode", byKey["bs_short"].Value);
        Assert.Contains("long ne", byKey["bs_long"].Value);
        // AD remains in ASCII regardless of language: clipboard targets are usually external apps
        Assert.Equal("2026-04-03", byKey["ad_iso"].Value);
        Assert.Equal("3 Apr 2026", byKey["ad_long"].Value);
    }

    [Fact]
    public void Build_OutOfRangeBsDate_ReturnsEmpty()
    {
        var adapter = new FakeNepaliDateAdapter();
        var loc     = Loc("en");

        // Year 1900 is below the supported range; FakeNepaliDateAdapter.BsToAd returns null
        var opts = DateFormatter.Build(1900, 1, 1, adapter, loc, isNepali: false);

        Assert.Empty(opts);
    }

    [Fact]
    public void Build_NullAdapter_Throws()
    {
        var loc = Loc("en");
        Assert.Throws<ArgumentNullException>(() =>
            DateFormatter.Build(2082, 12, 20, null!, loc, isNepali: false));
    }

    [Fact]
    public void Build_NullLocalization_Throws()
    {
        var adapter = new FakeNepaliDateAdapter();
        Assert.Throws<ArgumentNullException>(() =>
            DateFormatter.Build(2082, 12, 20, adapter, null!, isNepali: false));
    }
}
