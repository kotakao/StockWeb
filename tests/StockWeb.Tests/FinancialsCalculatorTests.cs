using StockWeb.Services;

namespace StockWeb.Tests;

/// <summary>
/// 指標卡的季度損益比率計算：毛利率＝毛利/營收、營益率＝營益/營收（皆 %）；
/// 營收缺漏或為 0 時回 null（避免除以零），分子缺漏亦回 null。
/// </summary>
public class FinancialsCalculatorTests
{
    [Fact]
    public void GrossMargin_ComputesPercent()
        => Assert.Equal(25.0, FinancialsCalculator.GrossMargin(1000, 250)!.Value, 6);

    [Fact]
    public void OperatingMargin_ComputesPercent()
        => Assert.Equal(10.0, FinancialsCalculator.OperatingMargin(1000, 100)!.Value, 6);

    [Fact]
    public void GrossMargin_CanBeNegative()
        => Assert.Equal(-5.0, FinancialsCalculator.GrossMargin(1000, -50)!.Value, 6);

    [Theory]
    [InlineData(null, 250.0)]   // 營收缺漏
    [InlineData(0.0, 250.0)]    // 營收為 0
    [InlineData(1000.0, null)]  // 分子缺漏
    public void Margin_MissingOrZeroInputs_ReturnsNull(double? revenue, double? numerator)
    {
        Assert.Null(FinancialsCalculator.GrossMargin(revenue, numerator));
        Assert.Null(FinancialsCalculator.OperatingMargin(revenue, numerator));
    }
}
