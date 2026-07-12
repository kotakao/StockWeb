using StockWeb.Models;
using StockWeb.Services;

namespace StockWeb.Tests;

/// <summary>
/// 市場寬度計算測試，案例與數字對齊 StockDCbot tests/test_analysis.py 的 MarketBreadthPureTest。
/// 輸入 rows 為日期新到舊（比照 get_market_daily）：先以舊到新建立再反轉。
/// </summary>
public class MarketBreadthCalculatorTests
{
    private static MarketDailyRow Md(int day, double? up = null, double? down = null,
        double? foreign = null, double? turnover = null)
        => new()
        {
            Date = $"2026-06-{day:D2}",
            UpCount = up,
            DownCount = down,
            ForeignNet = foreign,
            Turnover = turnover,
        };

    // get_market_daily 回傳日期新到舊；測試資料以舊到新建立後反轉。
    private static List<MarketDailyRow> Desc(IEnumerable<MarketDailyRow> ascending)
        => ascending.Reverse().ToList();

    // ---- A/D Line ----

    [Fact]
    public void AdLine_TrendUp_AndRecentWindow()
    {
        var asc = Enumerable.Range(1, 25).Select(i => Md(i, up: 1000, down: 500)); // 每日淨 +500
        var data = MarketBreadthCalculator.AdvanceDeclineLine(Desc(asc));

        Assert.Equal(25, data.SampleDays);
        Assert.Equal(10, data.Series.Count);           // 只顯示近 10 日
        Assert.Equal("升", data.Trend);                // 與 20 日前比較，累積上升
        Assert.Equal(
            data.Series.Select(p => p.Cumulative).OrderBy(v => v).ToList(),
            data.Series.Select(p => p.Cumulative).ToList());
    }

    [Fact]
    public void AdLine_TrendDown_AndFlat()
    {
        var down = Enumerable.Range(1, 25).Select(i => Md(i, up: 200, down: 800));
        Assert.Equal("降", MarketBreadthCalculator.AdvanceDeclineLine(Desc(down)).Trend);

        var flat = Enumerable.Range(1, 25).Select(i => Md(i, up: 500, down: 500));
        Assert.Equal("持平", MarketBreadthCalculator.AdvanceDeclineLine(Desc(flat)).Trend);
    }

    [Fact]
    public void AdLine_InsufficientForTrend()
    {
        var asc = Enumerable.Range(1, 15).Select(i => Md(i, up: 1000, down: 500)); // 15 日 < 21
        var data = MarketBreadthCalculator.AdvanceDeclineLine(Desc(asc));

        Assert.Null(data.Trend);                        // 不足 20 日前基準
        Assert.Equal(10, data.Series.Count);
    }

    [Fact]
    public void AdLine_NoCounts_Degrades()
    {
        var data = MarketBreadthCalculator.AdvanceDeclineLine(new List<MarketDailyRow> { Md(1) });

        Assert.Equal(0, data.SampleDays);
        Assert.Empty(data.Series);
    }

    // ---- 量能溫度 ----

    [Fact]
    public void VolumeTemperature_Multiple()
    {
        var today = Md(21, turnover: 2e11);
        var prior = Enumerable.Range(1, 20).Select(i => Md(i, turnover: 1e11)); // 近 20 日均值 1e11
        var rows = new List<MarketDailyRow> { today };
        rows.AddRange(Desc(prior));                     // 新到舊

        var data = MarketBreadthCalculator.VolumeTemperature(rows);

        Assert.Equal(20, data.SampleDays);
        Assert.Equal(2.0, data.Multiple);
    }

    [Fact]
    public void VolumeTemperature_Insufficient()
    {
        var data = MarketBreadthCalculator.VolumeTemperature(
            new List<MarketDailyRow> { Md(1, turnover: 1e11) });          // 無前值可比

        Assert.Equal(0, data.SampleDays);
        Assert.Null(data.Multiple);
    }

    // ---- 外資動向 ----

    [Fact]
    public void ForeignFlowTrend_Up()
    {
        var asc = Enumerable.Range(1, 10).Select(i => Md(i, foreign: 1_000_000)); // 每日淨買超 → 累積升
        var data = MarketBreadthCalculator.ForeignFlowTrend(Desc(asc));

        Assert.Equal(10, data.SampleDays);
        Assert.Equal(5, data.Series.Count);             // 近 5 日
        Assert.Equal("升", data.Trend);
        Assert.Equal(1000, data.Series[^1].Net);        // 1,000,000 股 / 1000
    }

    [Fact]
    public void ForeignFlowTrend_NoData()
    {
        var data = MarketBreadthCalculator.ForeignFlowTrend(new List<MarketDailyRow> { Md(1) });

        Assert.Equal(0, data.SampleDays);
        Assert.Null(data.Trend);
    }
}
