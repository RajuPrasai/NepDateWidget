using NepDateWidget.Converters;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace NepDateWidget.Tests;

public sealed class ConverterTests
{
    // ── BoolToVisibilityConverter ──────────────────────────────────────────────

    [Fact]
    public void BoolToVisibility_True_ReturnsVisible()
    {
        var c = new BoolToVisibilityConverter();
        var result = c.Convert(true, typeof(Visibility), null!, CultureInfo.InvariantCulture);
        Assert.Equal(Visibility.Visible, result);
    }

    [Fact]
    public void BoolToVisibility_False_ReturnsCollapsed()
    {
        var c = new BoolToVisibilityConverter();
        var result = c.Convert(false, typeof(Visibility), null!, CultureInfo.InvariantCulture);
        Assert.Equal(Visibility.Collapsed, result);
    }

    [Fact]
    public void BoolToVisibility_Inverted_True_ReturnsCollapsed()
    {
        var c = new BoolToVisibilityConverter { Invert = true };
        var result = c.Convert(true, typeof(Visibility), null!, CultureInfo.InvariantCulture);
        Assert.Equal(Visibility.Collapsed, result);
    }

    [Fact]
    public void BoolToVisibility_Inverted_False_ReturnsVisible()
    {
        var c = new BoolToVisibilityConverter { Invert = true };
        var result = c.Convert(false, typeof(Visibility), null!, CultureInfo.InvariantCulture);
        Assert.Equal(Visibility.Visible, result);
    }

    [Fact]
    public void BoolToVisibility_NonBoolInput_ReturnsCollapsed()
    {
        var c = new BoolToVisibilityConverter();
        var result = c.Convert("not a bool", typeof(Visibility), null!, CultureInfo.InvariantCulture);
        Assert.Equal(Visibility.Collapsed, result);
    }

    [Fact]
    public void BoolToVisibility_ConvertBack_Visible_ReturnsTrue()
    {
        var c = new BoolToVisibilityConverter();
        var result = c.ConvertBack(Visibility.Visible, typeof(bool), null!, CultureInfo.InvariantCulture);
        Assert.Equal(true, result);
    }

    [Fact]
    public void BoolToVisibility_ConvertBack_Collapsed_ReturnsFalse()
    {
        var c = new BoolToVisibilityConverter();
        var result = c.ConvertBack(Visibility.Collapsed, typeof(bool), null!, CultureInfo.InvariantCulture);
        Assert.Equal(false, result);
    }

    [Fact]
    public void BoolToVisibility_ConvertBack_Inverted()
    {
        var c = new BoolToVisibilityConverter { Invert = true };
        var result = c.ConvertBack(Visibility.Visible, typeof(bool), null!, CultureInfo.InvariantCulture);
        Assert.Equal(false, result); // Inverted: Visible = false
    }

    // ── IntEqualConverter ─────────────────────────────────────────────────────

    [Fact]
    public void IntEqual_MatchingValue_ReturnsTrue()
    {
        var c = new IntEqualConverter();
        var result = c.Convert(2, typeof(bool), "2", CultureInfo.InvariantCulture);
        Assert.Equal(true, result);
    }

    [Fact]
    public void IntEqual_NonMatchingValue_ReturnsFalse()
    {
        var c = new IntEqualConverter();
        var result = c.Convert(3, typeof(bool), "2", CultureInfo.InvariantCulture);
        Assert.Equal(false, result);
    }

    [Fact]
    public void IntEqual_NonIntValue_ReturnsFalse()
    {
        var c = new IntEqualConverter();
        var result = c.Convert("not int", typeof(bool), "2", CultureInfo.InvariantCulture);
        Assert.Equal(false, result);
    }

    [Fact]
    public void IntEqual_NonStringParameter_ReturnsFalse()
    {
        var c = new IntEqualConverter();
        var result = c.Convert(2, typeof(bool), 2, CultureInfo.InvariantCulture);
        Assert.Equal(false, result);
    }

    [Fact]
    public void IntEqual_ConvertBack_True_ReturnsTarget()
    {
        var c = new IntEqualConverter();
        var result = c.ConvertBack(true, typeof(int), "5", CultureInfo.InvariantCulture);
        Assert.Equal(5, result);
    }

    [Fact]
    public void IntEqual_ConvertBack_False_ReturnsDoNothing()
    {
        var c = new IntEqualConverter();
        var result = c.ConvertBack(false, typeof(int), "5", CultureInfo.InvariantCulture);
        Assert.Equal(Binding.DoNothing, result);
    }

    // ── StrengthBarWidthConverter ─────────────────────────────────────────────

    [Fact]
    public void StrengthBar_50Percent_HalfWidth()
    {
        var c = new StrengthBarWidthConverter();
        var result = c.Convert(new object[] { 50.0, 200.0 }, typeof(double), null!, CultureInfo.InvariantCulture);
        Assert.Equal(100.0, result);
    }

    [Fact]
    public void StrengthBar_100Percent_FullWidth()
    {
        var c = new StrengthBarWidthConverter();
        var result = c.Convert(new object[] { 100.0, 200.0 }, typeof(double), null!, CultureInfo.InvariantCulture);
        Assert.Equal(200.0, result);
    }

    [Fact]
    public void StrengthBar_0Percent_ZeroWidth()
    {
        var c = new StrengthBarWidthConverter();
        var result = c.Convert(new object[] { 0.0, 200.0 }, typeof(double), null!, CultureInfo.InvariantCulture);
        Assert.Equal(0.0, result);
    }

    [Fact]
    public void StrengthBar_NegativePercent_ClampsToZero()
    {
        var c = new StrengthBarWidthConverter();
        var result = c.Convert(new object[] { -10.0, 200.0 }, typeof(double), null!, CultureInfo.InvariantCulture);
        Assert.Equal(0.0, result);
    }

    [Fact]
    public void StrengthBar_Over100Percent_ClampsToTotal()
    {
        var c = new StrengthBarWidthConverter();
        var result = c.Convert(new object[] { 150.0, 200.0 }, typeof(double), null!, CultureInfo.InvariantCulture);
        Assert.Equal(200.0, result);
    }

    [Fact]
    public void StrengthBar_ZeroTotalWidth_ReturnsZero()
    {
        var c = new StrengthBarWidthConverter();
        var result = c.Convert(new object[] { 50.0, 0.0 }, typeof(double), null!, CultureInfo.InvariantCulture);
        Assert.Equal(0.0, result);
    }

    [Fact]
    public void StrengthBar_NegativeTotalWidth_ReturnsZero()
    {
        var c = new StrengthBarWidthConverter();
        var result = c.Convert(new object[] { 50.0, -100.0 }, typeof(double), null!, CultureInfo.InvariantCulture);
        Assert.Equal(0.0, result);
    }

    [Fact]
    public void StrengthBar_InsufficientValues_ReturnsZero()
    {
        var c = new StrengthBarWidthConverter();
        var result = c.Convert(new object[] { 50.0 }, typeof(double), null!, CultureInfo.InvariantCulture);
        Assert.Equal(0.0, result);
    }

    [Fact]
    public void StrengthBar_NonDoublePercent_ReturnsZero()
    {
        var c = new StrengthBarWidthConverter();
        var result = c.Convert(new object[] { "50", 200.0 }, typeof(double), null!, CultureInfo.InvariantCulture);
        Assert.Equal(0.0, result);
    }

    [Fact]
    public void StrengthBar_ConvertBack_Throws()
    {
        var c = new StrengthBarWidthConverter();
        Assert.Throws<NotSupportedException>(() =>
            c.ConvertBack(100.0, new[] { typeof(double), typeof(double) }, null!, CultureInfo.InvariantCulture));
    }
}
