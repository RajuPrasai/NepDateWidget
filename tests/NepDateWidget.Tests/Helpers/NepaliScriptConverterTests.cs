using NepDateWidget.Helpers;

namespace NepDateWidget.Tests.Helpers;

public sealed class NepaliScriptConverterTests
{
    // ── ToNepaliDigits ────────────────────────────────────────────────────────

    [Theory]
    [InlineData(0, "०")]
    [InlineData(5, "५")]
    [InlineData(2082, "२०८२")]
    [InlineData(123456789, "१२३४५६७८९")]
    public void ToNepaliDigits_ConvertsCorrectly(int input, string expected)
    {
        Assert.Equal(expected, NepaliScriptConverter.ToNepaliDigits(input));
    }

    // ── RomanToDevanagari ─────────────────────────────────────────────────────

    [Theory]
    [InlineData("", "")]
    [InlineData("ka", "क")]
    [InlineData("kha", "ख")]
    [InlineData("ga", "ग")]
    [InlineData("gha", "घ")]
    [InlineData("cha", "च")]
    [InlineData("chha", "छ")]
    [InlineData("ta", "त")]
    [InlineData("tha", "थ")]
    [InlineData("da", "द")]
    [InlineData("dha", "ध")]
    [InlineData("na", "न")]
    [InlineData("pa", "प")]
    [InlineData("pha", "फ")]
    [InlineData("ba", "ब")]
    [InlineData("bha", "भ")]
    [InlineData("ma", "म")]
    [InlineData("ya", "य")]
    [InlineData("ra", "र")]
    [InlineData("la", "ल")]
    [InlineData("sa", "स")]
    [InlineData("ha", "ह")]
    public void RomanToDevanagari_SingleConsonantVowel(string roman, string expected)
    {
        Assert.Equal(expected, NepaliScriptConverter.RomanToDevanagari(roman));
    }

    [Theory]
    [InlineData("namaste", "नमस्ते")]
    [InlineData("nepal", "नेपल")]
    public void RomanToDevanagari_Words(string roman, string expected)
    {
        Assert.Equal(expected, NepaliScriptConverter.RomanToDevanagari(roman));
    }

    [Fact]
    public void RomanToDevanagari_NullInput_ReturnsEmpty()
    {
        Assert.Equal(string.Empty, NepaliScriptConverter.RomanToDevanagari(null!));
    }

    [Fact]
    public void RomanToDevanagari_PreservesWhitespaceAndDigits()
    {
        var result = NepaliScriptConverter.RomanToDevanagari("ka 123 ga");
        Assert.Contains("123", result);
        Assert.Contains(" ", result);
    }

    [Fact]
    public void RomanToDevanagari_PreservesSlashSeparator()
    {
        var result = NepaliScriptConverter.RomanToDevanagari("ka/ga");
        Assert.Contains("/", result);
    }

    // ── Vowel patterns ────────────────────────────────────────────────────────

    [Theory]
    [InlineData("aa", "आ")]
    [InlineData("i", "इ")]
    [InlineData("ii", "ई")]
    [InlineData("u", "उ")]
    [InlineData("uu", "ऊ")]
    [InlineData("e", "ए")]
    [InlineData("ai", "ऐ")]
    [InlineData("o", "ओ")]
    [InlineData("au", "औ")]
    public void RomanToDevanagari_StandaloneVowels(string roman, string expected)
    {
        Assert.Equal(expected, NepaliScriptConverter.RomanToDevanagari(roman));
    }

    // ── Special conjuncts ─────────────────────────────────────────────────────

    [Theory]
    [InlineData("ksha", "क्ष")]
    [InlineData("gya", "ज्ञ")]
    [InlineData("shra", "श्र")]
    public void RomanToDevanagari_Conjuncts(string roman, string expected)
    {
        Assert.Equal(expected, NepaliScriptConverter.RomanToDevanagari(roman));
    }

    // ── DevanagariToRoman ─────────────────────────────────────────────────────

    [Theory]
    [InlineData("क", "ka")]
    [InlineData("ख", "kha")]
    [InlineData("न", "na")]
    [InlineData("म", "ma")]
    public void DevanagariToRoman_SingleConsonant(string deva, string expected)
    {
        Assert.Equal(expected, NepaliScriptConverter.DevanagariToRoman(deva));
    }

    [Fact]
    public void DevanagariToRoman_NullInput_ReturnsEmpty()
    {
        Assert.Equal(string.Empty, NepaliScriptConverter.DevanagariToRoman(null!));
    }

    [Fact]
    public void DevanagariToRoman_EmptyInput_ReturnsEmpty()
    {
        Assert.Equal(string.Empty, NepaliScriptConverter.DevanagariToRoman(""));
    }

    [Theory]
    [InlineData("अ", "a")]
    [InlineData("आ", "aa")]
    [InlineData("इ", "i")]
    [InlineData("ई", "ii")]
    [InlineData("उ", "u")]
    [InlineData("ऊ", "uu")]
    [InlineData("ए", "e")]
    [InlineData("ऐ", "ai")]
    [InlineData("ओ", "o")]
    [InlineData("औ", "au")]
    public void DevanagariToRoman_StandaloneVowels(string deva, string expected)
    {
        Assert.Equal(expected, NepaliScriptConverter.DevanagariToRoman(deva));
    }

    [Fact]
    public void DevanagariToRoman_ConsonantWithMatra()
    {
        // का = ka + ा (aa matra)
        Assert.Equal("kaa", NepaliScriptConverter.DevanagariToRoman("का"));
        // कि = ka + ि (i matra)
        Assert.Equal("ki", NepaliScriptConverter.DevanagariToRoman("कि"));
    }

    [Fact]
    public void DevanagariToRoman_Halant_JoinsConsonants()
    {
        // क्ष = क + ् + ष
        var result = NepaliScriptConverter.DevanagariToRoman("क्ष");
        Assert.Equal("ksha", result);
    }

    // ── Round-trip (approximate) ──────────────────────────────────────────────

    [Theory]
    [InlineData("ka")]
    [InlineData("ma")]
    [InlineData("na")]
    public void RoundTrip_SimpleConsonantVowel(string roman)
    {
        var deva = NepaliScriptConverter.RomanToDevanagari(roman);
        var back = NepaliScriptConverter.DevanagariToRoman(deva);
        Assert.Equal(roman, back);
    }
}
