using NepDateWidget.Services;

namespace NepDateWidget.Tests.Services;

public class ConversionServiceTests
{
    private static ConversionService CreateService()
        => new(new FakeNepaliDateAdapter());

    // ── AdToBs - happy path ───────────────────────────────────────────────────

    [Fact]
    public void AdToBs_ValidDate_ReturnsSuccess()
    {
        var svc    = CreateService();
        var result = svc.AdToBs(2026, 4, 3);

        Assert.True(result.IsSuccess);
        Assert.Equal("2082/12/20", result.Result);
    }

    [Fact]
    public void AdToBs_ValidDate_ResultLong_IsNonEmpty()
    {
        var result = CreateService().AdToBs(2026, 4, 3);
        Assert.True(result.IsSuccess);
        Assert.NotEmpty(result.ResultLong);
    }

    // ── AdToBs - validation errors ────────────────────────────────────────────

    [Theory]
    [InlineData(0,    1,  1)]    // year < 1
    [InlineData(10000,1,  1)]    // year > 9999
    public void AdToBs_InvalidYear_ReturnsFail(int year, int month, int day)
    {
        var result = CreateService().AdToBs(year, month, day);
        Assert.False(result.IsSuccess);
        Assert.NotEmpty(result.ErrorMessage);
    }

    [Theory]
    [InlineData(2026,  0, 1)]    // month 0
    [InlineData(2026, 13, 1)]    // month 13
    public void AdToBs_InvalidMonth_ReturnsFail(int year, int month, int day)
    {
        var result = CreateService().AdToBs(year, month, day);
        Assert.False(result.IsSuccess);
        Assert.NotEmpty(result.ErrorMessage);
    }

    [Theory]
    [InlineData(2026, 1,  0)]    // day 0
    [InlineData(2026, 1, 32)]    // day 32
    public void AdToBs_InvalidDay_ReturnsFail(int year, int month, int day)
    {
        var result = CreateService().AdToBs(year, month, day);
        Assert.False(result.IsSuccess);
        Assert.NotEmpty(result.ErrorMessage);
    }

    // ── BsToAd - happy path ───────────────────────────────────────────────────

    [Fact]
    public void BsToAd_ValidDate_ReturnsSuccess()
    {
        var result = CreateService().BsToAd(2082, 12, 20);

        Assert.True(result.IsSuccess);
        Assert.Equal("2026-04-03", result.Result);
    }

    [Fact]
    public void BsToAd_ValidDate_ResultLong_IsNonEmpty()
    {
        var result = CreateService().BsToAd(2082, 12, 20);
        Assert.True(result.IsSuccess);
        Assert.NotEmpty(result.ResultLong);
    }

    // ── BsToAd - validation errors ────────────────────────────────────────────

    [Theory]
    [InlineData(1900, 1, 1)]    // year below min
    [InlineData(2200, 1, 1)]    // year above max
    public void BsToAd_InvalidYear_ReturnsFail(int year, int month, int day)
    {
        var result = CreateService().BsToAd(year, month, day);
        Assert.False(result.IsSuccess);
        Assert.NotEmpty(result.ErrorMessage);
    }

    [Theory]
    [InlineData(2082,  0, 1)]    // month 0
    [InlineData(2082, 13, 1)]    // month 13
    public void BsToAd_InvalidMonth_ReturnsFail(int year, int month, int day)
    {
        var result = CreateService().BsToAd(year, month, day);
        Assert.False(result.IsSuccess);
        Assert.NotEmpty(result.ErrorMessage);
    }

    [Theory]
    [InlineData(2082, 1,  0)]    // day 0
    [InlineData(2082, 1, 33)]    // day 33
    public void BsToAd_InvalidDay_ReturnsFail(int year, int month, int day)
    {
        var result = CreateService().BsToAd(year, month, day);
        Assert.False(result.IsSuccess);
        Assert.NotEmpty(result.ErrorMessage);
    }

    // ── Error message content ─────────────────────────────────────────────────

    [Fact]
    public void AdToBs_Fail_HasNonEmptyErrorMessage()
    {
        var result = CreateService().AdToBs(0, 1, 1);
        Assert.False(result.IsSuccess);
        Assert.False(string.IsNullOrWhiteSpace(result.ErrorMessage));
    }

    [Fact]
    public void BsToAd_Fail_HasNonEmptyErrorMessage()
    {
        var result = CreateService().BsToAd(1900, 1, 1);
        Assert.False(result.IsSuccess);
        Assert.False(string.IsNullOrWhiteSpace(result.ErrorMessage));
    }

    // ── ConversionResult factories ────────────────────────────────────────────

    [Fact]
    public void ConversionResult_Ok_IsSuccess_True()
    {
        var r = NepDateWidget.Models.ConversionResult.Ok("2082/12/20", "Chaitra 20, 2082");
        Assert.True(r.IsSuccess);
        Assert.Equal("2082/12/20", r.Result);
        Assert.Equal("Chaitra 20, 2082", r.ResultLong);
    }

    [Fact]
    public void ConversionResult_Fail_IsSuccess_False()
    {
        var r = NepDateWidget.Models.ConversionResult.Fail("Bad input");
        Assert.False(r.IsSuccess);
        Assert.Equal("Bad input", r.ErrorMessage);
    }
}
