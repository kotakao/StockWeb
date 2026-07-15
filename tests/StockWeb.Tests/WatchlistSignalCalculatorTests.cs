using StockWeb.Models;
using StockWeb.Services;

namespace StockWeb.Tests;

/// <summary>狀態訊號純函數測試：方向（正負零/資料不足）與接近 52 週高低（門檻邊界）。</summary>
public class WatchlistSignalCalculatorTests
{
    [Theory]
    [InlineData(1500.0, SignalTrend.Up)]
    [InlineData(-800.0, SignalTrend.Down)]
    [InlineData(0.0, SignalTrend.Flat)]
    public void TrendOf_ClassifiesBySign(double value, SignalTrend expected)
        => Assert.Equal(expected, WatchlistSignalCalculator.TrendOf(value));

    [Fact]
    public void TrendOf_Null_IsUnknown()
        => Assert.Equal(SignalTrend.Unknown, WatchlistSignalCalculator.TrendOf(null));

    [Fact]
    public void NearFiftyTwoWeek_WithinFivePercentOfHigh_IsNearHigh()
    {
        // 高 100、低 50；收盤 96 ≥ 100×0.95 → 近高。
        Assert.Equal(RangeSignal.NearHigh, WatchlistSignalCalculator.NearFiftyTwoWeek(96, 100, 50));
    }

    [Fact]
    public void NearFiftyTwoWeek_WithinFivePercentOfLow_IsNearLow()
    {
        // 低 50、高 100；收盤 52 ≤ 50×1.05=52.5 → 近低。
        Assert.Equal(RangeSignal.NearLow, WatchlistSignalCalculator.NearFiftyTwoWeek(52, 100, 50));
    }

    [Fact]
    public void NearFiftyTwoWeek_InMiddle_IsNone()
    {
        // 收盤 75 距高低皆超過 5% → None。
        Assert.Equal(RangeSignal.None, WatchlistSignalCalculator.NearFiftyTwoWeek(75, 100, 50));
    }

    [Fact]
    public void NearFiftyTwoWeek_ExactlyAtThreshold_CountsAsNear()
    {
        // 邊界：收盤 = 高×0.95 恰好視為近高（>= 判斷）。
        Assert.Equal(RangeSignal.NearHigh, WatchlistSignalCalculator.NearFiftyTwoWeek(95, 100, 50));
    }

    [Fact]
    public void NearFiftyTwoWeek_MissingCloseOrExtremes_IsNone()
    {
        Assert.Equal(RangeSignal.None, WatchlistSignalCalculator.NearFiftyTwoWeek(null, 100, 50));
        Assert.Equal(RangeSignal.None, WatchlistSignalCalculator.NearFiftyTwoWeek(96, null, null));
    }
}
