using StockWeb.Api;

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
}
