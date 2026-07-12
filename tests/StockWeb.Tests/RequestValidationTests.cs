using StockWeb.Api;
using StockWeb.Models;

namespace StockWeb.Tests;

public class RequestValidationTests
{
    [Theory]
    [InlineData("2330")]   // 4 位數字
    [InlineData("00631L")] // 6 位英數（ETF）
    [InlineData("AAPL")]   // 英文
    public void TryValidateCode_ValidCode_ReturnsTrue(string code)
    {
        Assert.True(RequestValidation.TryValidateCode(code, out var error));
        Assert.Null(error);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("233")]      // 太短
    [InlineData("1234567")]  // 太長
    [InlineData("23 30")]    // 含空白
    [InlineData("23#0")]     // 含符號
    public void TryValidateCode_InvalidCode_ReturnsFalseWithError(string? code)
    {
        Assert.False(RequestValidation.TryValidateCode(code, out var error));
        Assert.NotNull(error);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(60)]
    [InlineData(252)]
    public void TryValidateDays_WithinRange_ReturnsTrue(int days)
    {
        Assert.True(RequestValidation.TryValidateDays(days, out var error));
        Assert.Null(error);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(253)]
    public void TryValidateDays_OutOfRange_ReturnsFalse(int days)
    {
        Assert.False(RequestValidation.TryValidateDays(days, out var error));
        Assert.NotNull(error);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(252)]
    public void TryValidateMonths_WithinRange_ReturnsTrue(int months)
    {
        Assert.True(RequestValidation.TryValidateMonths(months, out _));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(253)]
    public void TryValidateMonths_OutOfRange_ReturnsFalse(int months)
    {
        Assert.False(RequestValidation.TryValidateMonths(months, out var error));
        Assert.NotNull(error);
    }

    [Theory]
    [InlineData("2026-07-12")]
    [InlineData("2025-01-01")]
    public void TryValidateDate_ValidDate_ReturnsTrue(string date)
    {
        Assert.True(RequestValidation.TryValidateDate(date, out var error));
        Assert.Null(error);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("2026-13-01")]  // 月份不合法
    [InlineData("2026-02-30")]  // 日不合法
    [InlineData("2026/07/12")]  // 分隔符錯誤
    [InlineData("20260712")]    // 無分隔符
    public void TryValidateDate_InvalidDate_ReturnsFalseWithError(string? date)
    {
        Assert.False(RequestValidation.TryValidateDate(date, out var error));
        Assert.NotNull(error);
    }

    [Theory]
    [InlineData("2026-08", 2026, 8)]
    [InlineData("2025-01", 2025, 1)]
    [InlineData("2026-12", 2026, 12)]
    public void TryValidateMonth_ValidMonth_ReturnsFirstDay(string month, int year, int m)
    {
        Assert.True(RequestValidation.TryValidateMonth(month, out var start, out var error));
        Assert.Null(error);
        Assert.Equal(new DateOnly(year, m, 1), start);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("2026-13")]     // 月份不合法
    [InlineData("2026-00")]     // 月份不合法
    [InlineData("2026/08")]     // 分隔符錯誤
    [InlineData("2026-08-01")]  // 多帶日
    [InlineData("202608")]      // 無分隔符
    public void TryValidateMonth_InvalidMonth_ReturnsFalseWithError(string? month)
    {
        Assert.False(RequestValidation.TryValidateMonth(month, out _, out var error));
        Assert.NotNull(error);
    }

    [Fact]
    public void TryValidateScreener_EmptyCriteria_IsValid()
    {
        Assert.True(RequestValidation.TryValidateScreener(new ScreenerCriteria(), out var error));
        Assert.Null(error);
    }

    [Theory]
    [InlineData("TWSE")]
    [InlineData("TPEX")]
    [InlineData("ALL")]
    [InlineData("twse")]   // 大小寫不敏感
    public void TryValidateScreener_ValidMarket_ReturnsTrue(string market)
    {
        Assert.True(RequestValidation.TryValidateScreener(new ScreenerCriteria { Market = market }, out _));
    }

    [Theory]
    [InlineData("NYSE")]
    [InlineData("tw")]
    public void TryValidateScreener_InvalidMarket_ReturnsFalse(string market)
    {
        Assert.False(RequestValidation.TryValidateScreener(new ScreenerCriteria { Market = market }, out var error));
        Assert.NotNull(error);
    }

    [Theory]
    [InlineData(0)]     // 須 >= 1
    [InlineData(-1)]
    [InlineData(253)]   // 超過上限 252
    public void TryValidateScreener_BuyDaysOutOfRange_ReturnsFalse(int days)
    {
        Assert.False(RequestValidation.TryValidateScreener(new ScreenerCriteria { ForeignBuyDays = days }, out var error));
        Assert.NotNull(error);
    }

    [Fact]
    public void TryValidateScreener_NonPositivePeMax_ReturnsFalse()
    {
        Assert.False(RequestValidation.TryValidateScreener(new ScreenerCriteria { PeMax = 0 }, out var error));
        Assert.NotNull(error);
    }

    [Fact]
    public void TryValidateScreener_NegativeDividendYield_ReturnsFalse()
    {
        Assert.False(RequestValidation.TryValidateScreener(new ScreenerCriteria { DividendYieldMin = -1 }, out var error));
        Assert.NotNull(error);
    }

    [Fact]
    public void TryValidateScreener_NegativeRevenueYoy_IsAllowed()
    {
        // 營收衰退（負 YoY）也是合法篩選條件。
        Assert.True(RequestValidation.TryValidateScreener(new ScreenerCriteria { RevenueYoyMin = -20 }, out _));
    }

    [Fact]
    public void TryValidateScreener_NonFiniteValue_ReturnsFalse()
    {
        Assert.False(RequestValidation.TryValidateScreener(new ScreenerCriteria { VolumeMultipleMin = double.NaN }, out var error));
        Assert.NotNull(error);
    }
}
