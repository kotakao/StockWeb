using StockWeb.Models;
using StockWeb.Services;

namespace StockWeb.Tests;

/// <summary>
/// 日 K 聚合為週／月／年 K 的純邏輯測試：開＝首日開、收＝末日收、高＝區間最高、低＝區間最低、量＝加總；
/// 涵蓋跨週／跨月／跨年邊界、還原後再聚合的組合、不足一根的殘週。輸入須為由舊到新排序。
/// </summary>
public class QuoteAggregatorTests
{
    private static StockQuote Q(string date, double open, double high, double low, double close, double volume)
        => new(date, open, high, low, close, volume);

    [Fact]
    public void Daily_ReturnsInputUnchanged()
    {
        var daily = new[] { Q("2026-06-01", 10, 11, 9, 10, 100) };
        Assert.Same(daily, QuoteAggregator.Aggregate(daily, QuotePeriod.Daily));
    }

    [Fact]
    public void Empty_ReturnsEmpty()
    {
        Assert.Empty(QuoteAggregator.Aggregate(Array.Empty<StockQuote>(), QuotePeriod.Weekly));
    }

    [Fact]
    public void Weekly_AggregatesOhlcv_WithinOneWeek()
    {
        // 2026-06-01(一)～06-03(三)同一 ISO 週。
        var daily = new[]
        {
            Q("2026-06-01", 10, 15, 9, 12, 100),
            Q("2026-06-02", 12, 18, 11, 17, 200),
            Q("2026-06-03", 17, 20, 16, 19, 150),
        };

        var bars = QuoteAggregator.Aggregate(daily, QuotePeriod.Weekly);

        var bar = Assert.Single(bars);
        Assert.Equal("2026-06-03", bar.Date);   // 以區間最後交易日為時間
        Assert.Equal(10, bar.Open);              // 首日開
        Assert.Equal(19, bar.Close);             // 末日收
        Assert.Equal(20, bar.High);              // 區間最高
        Assert.Equal(9, bar.Low);                // 區間最低
        Assert.Equal(450, bar.Volume);           // 量加總
    }

    [Fact]
    public void Weekly_SplitsAcrossWeekBoundary()
    {
        // 06-05(五) 與 06-08(一) 屬相鄰但不同 ISO 週。
        var daily = new[]
        {
            Q("2026-06-04", 10, 10, 10, 10, 100),
            Q("2026-06-05", 11, 11, 11, 11, 100),
            Q("2026-06-08", 20, 20, 20, 20, 100),
        };

        var bars = QuoteAggregator.Aggregate(daily, QuotePeriod.Weekly);

        Assert.Equal(2, bars.Count);
        Assert.Equal(new[] { "2026-06-05", "2026-06-08" }, bars.Select(b => b.Date));
    }

    [Fact]
    public void Weekly_GroupsCrossCalendarYear_ByIsoWeek()
    {
        // 2025-12-31(三) 與 2026-01-01(四) 同屬 ISO 2026 第 1 週 → 合為一根。
        var daily = new[]
        {
            Q("2025-12-31", 10, 12, 9, 11, 100),
            Q("2026-01-01", 11, 14, 10, 13, 200),
        };

        var bars = QuoteAggregator.Aggregate(daily, QuotePeriod.Weekly);

        var bar = Assert.Single(bars);
        Assert.Equal("2026-01-01", bar.Date);
        Assert.Equal(10, bar.Open);
        Assert.Equal(13, bar.Close);
        Assert.Equal(14, bar.High);
        Assert.Equal(9, bar.Low);
        Assert.Equal(300, bar.Volume);
    }

    [Fact]
    public void Monthly_SplitsAcrossMonthBoundary()
    {
        var daily = new[]
        {
            Q("2026-06-29", 10, 10, 10, 10, 100),
            Q("2026-06-30", 12, 13, 8, 9, 100),
            Q("2026-07-01", 20, 22, 19, 21, 100),
        };

        var bars = QuoteAggregator.Aggregate(daily, QuotePeriod.Monthly);

        Assert.Equal(2, bars.Count);
        var june = bars[0];
        Assert.Equal("2026-06-30", june.Date);
        Assert.Equal(10, june.Open);
        Assert.Equal(9, june.Close);
        Assert.Equal(13, june.High);
        Assert.Equal(8, june.Low);
        Assert.Equal(200, june.Volume);
        Assert.Equal("2026-07-01", bars[1].Date);
    }

    [Fact]
    public void Yearly_SplitsAcrossCalendarYear()
    {
        // 對比週 K：跨曆年在年 K 一定分兩根（即便屬同一 ISO 週）。
        var daily = new[]
        {
            Q("2025-12-31", 10, 12, 9, 11, 100),
            Q("2026-01-01", 11, 14, 10, 13, 200),
        };

        var bars = QuoteAggregator.Aggregate(daily, QuotePeriod.Yearly);

        Assert.Equal(2, bars.Count);
        Assert.Equal(new[] { "2025-12-31", "2026-01-01" }, bars.Select(b => b.Date));
    }

    [Fact]
    public void Weekly_ResidualPartialWeek_FormsOwnBar()
    {
        // 完整週（06-01～06-05）後接殘週（僅 06-08 一天）→ 殘週如實聚合為一根。
        var daily = new[]
        {
            Q("2026-06-01", 10, 10, 10, 10, 100),
            Q("2026-06-02", 10, 10, 10, 10, 100),
            Q("2026-06-03", 10, 10, 10, 10, 100),
            Q("2026-06-04", 10, 10, 10, 10, 100),
            Q("2026-06-05", 10, 10, 10, 10, 100),
            Q("2026-06-08", 20, 25, 18, 22, 300),
        };

        var bars = QuoteAggregator.Aggregate(daily, QuotePeriod.Weekly);

        Assert.Equal(2, bars.Count);
        var residual = bars[1];
        Assert.Equal("2026-06-08", residual.Date);
        Assert.Equal(20, residual.Open);
        Assert.Equal(22, residual.Close);
        Assert.Equal(25, residual.High);
        Assert.Equal(18, residual.Low);
        Assert.Equal(300, residual.Volume);
    }

    [Fact]
    public void Weekly_AdjustThenAggregate_ComposesCorrectly()
    {
        // 先還原再聚合：除息事件 06-08、現金 5 → 之前（第 1 週）各價 -5，第 2 週不動。
        var daily = new[]
        {
            Q("2026-06-01", 100, 100, 100, 100, 100),
            Q("2026-06-02", 100, 100, 100, 100, 100),
            Q("2026-06-08", 100, 100, 100, 100, 100),
        };
        var adjusted = AdjustedPriceService.Adjust(daily, new[] { new DividendAdjustment("2026-06-08", 5, 0) });

        var bars = QuoteAggregator.Aggregate(adjusted, QuotePeriod.Weekly);

        Assert.Equal(2, bars.Count);
        Assert.Equal(95, bars[0].Close);    // 第 1 週由還原後 95 聚合
        Assert.Equal(95, bars[0].Open);
        Assert.Equal(100, bars[1].Close);   // 除息當週不動
    }
}
