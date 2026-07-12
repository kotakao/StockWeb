using StockWeb.Models;

namespace StockWeb.Services;

/// <summary>
/// 市場寬度純計算：A/D Line、量能溫度、法人流向。演算法逐一對齊 StockDCbot analysis.py 的
/// advance_decline_line / volume_temperature / foreign_flow_trend（數值以 Python 版為準）。
///
/// 輸入 rows 依 get_market_daily 慣例為「日期新到舊」；各指標為獨立純函數，
/// 資料不足時以「樣本天數 0」或空序列降級，彼此不互相影響（比照各小節獨立容錯）。
/// 不依賴任何 ASP.NET 型別，可單元測試。
/// </summary>
public static class MarketBreadthCalculator
{
    public const int AdRecent = 10;      // A/D Line 顯示近 10 日
    public const int AdCompare = 20;     // 趨勢與 20 日前累積值比較
    public const int VolumeLookback = 20; // 量能溫度均值取近 20 日（不含當日）
    public const int ForeignLookback = 20; // 法人累積曲線取近 20 日
    public const int ForeignRecent = 5;    // 法人動向顯示近 5 日

    // Python round() 為 banker's rounding（四捨六入五成雙），以 ToEven 對齊。
    private static long Round(double value) => (long)Math.Round(value, MidpointRounding.ToEven);

    private static string? Trend(double change)
        => change > 0 ? "升" : change < 0 ? "降" : "持平";

    /// <summary>漲跌家數累積線（A/D Line）：以 up_count - down_count 逐日累積，原點為區間最舊日。</summary>
    public static AdLineResult AdvanceDeclineLine(
        IReadOnlyList<MarketDailyRow> rows, int recent = AdRecent, int compare = AdCompare)
    {
        // rows 為新到舊，反轉為舊到新後逐日累積；缺漲跌家數的日子略過。
        var ordered = Enumerable.Reverse(rows)
            .Where(r => r.UpCount is not null && r.DownCount is not null);

        var series = new List<AdLinePoint>();
        double cumulative = 0;
        foreach (var r in ordered)
        {
            var net = r.UpCount!.Value - r.DownCount!.Value;
            cumulative += net;
            series.Add(new AdLinePoint(r.Date, Round(r.UpCount.Value), Round(r.DownCount.Value), Round(net), Round(cumulative)));
        }

        string? trend = null;
        if (series.Count > compare)
            trend = Trend(series[^1].Cumulative - series[^(1 + compare)].Cumulative);

        return new AdLineResult(series.TakeLast(recent).ToList(), trend, compare, series.Count);
    }

    /// <summary>量能溫度：當日成交金額為近 days 日（不含當日）均值的幾倍。</summary>
    public static VolumeTemperatureResult VolumeTemperature(
        IReadOnlyList<MarketDailyRow> rows, int days = VolumeLookback)
    {
        // rows 新到舊；rows[0] 為當日，其後 days 日為比較基準。
        var turnovers = rows.Where(r => r.Turnover is not null).Select(r => r.Turnover!.Value).ToList();
        if (turnovers.Count == 0)
            return new VolumeTemperatureResult(null, null, null, 0);

        var today = turnovers[0];
        var prior = turnovers.Skip(1).Take(days).ToList();
        if (prior.Count == 0)
            return new VolumeTemperatureResult(today, null, null, 0);

        var average = prior.Average();
        double? multiple = average != 0 ? Math.Round(today / average, 2, MidpointRounding.ToEven) : null;
        return new VolumeTemperatureResult(today, average, multiple, prior.Count);
    }

    /// <summary>外資動向：foreign_net 近 days 日累積曲線（張），回傳近 recent 日與方向。</summary>
    public static FlowResult ForeignFlowTrend(
        IReadOnlyList<MarketDailyRow> rows, int days = ForeignLookback, int recent = ForeignRecent)
        => CumulativeFlow(rows, r => r.ForeignNet, days, recent);

    /// <summary>投信動向：trust_net 近 days 日累積曲線（與外資同演算法）。</summary>
    public static FlowResult TrustFlowTrend(
        IReadOnlyList<MarketDailyRow> rows, int days = ForeignLookback, int recent = ForeignRecent)
        => CumulativeFlow(rows, r => r.TrustNet, days, recent);

    /// <summary>自營商動向：dealer_net 近 days 日累積曲線（與外資同演算法）。</summary>
    public static FlowResult DealerFlowTrend(
        IReadOnlyList<MarketDailyRow> rows, int days = ForeignLookback, int recent = ForeignRecent)
        => CumulativeFlow(rows, r => r.DealerNet, days, recent);

    // foreign_flow_trend 的一般化核心：選 net 欄（股數）近 days 日累積、以 /1000 換算張、取近 recent 日算趨勢。
    private static FlowResult CumulativeFlow(
        IReadOnlyList<MarketDailyRow> rows, Func<MarketDailyRow, double?> selector, int days, int recent)
    {
        var ordered = Enumerable.Reverse(rows).Where(r => selector(r) is not null).ToList(); // 舊到新
        var window = ordered.Skip(Math.Max(0, ordered.Count - days)).ToList();               // 近 days 日

        var series = new List<FlowPoint>();
        double cumulative = 0;
        foreach (var r in window)
        {
            var net = selector(r)!.Value;
            cumulative += net;
            series.Add(new FlowPoint(r.Date, Round(net / 1000.0), Round(cumulative / 1000.0)));
        }

        var recentSeries = series.TakeLast(recent).ToList();
        string? trend = null;
        if (recentSeries.Count >= 2)
            trend = Trend(recentSeries[^1].Cumulative - recentSeries[0].Cumulative);

        return new FlowResult(recentSeries, trend, series.Count);
    }
}
