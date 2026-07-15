using StockWeb.Models;

namespace StockWeb.Services;

/// <summary>
/// 觀察名單狀態訊號的純函數（不依賴 ASP.NET 型別，可單元測試）。
/// 方向訊號僅取數值正負號；52 週高低以「已還原」序列的最高高價／最低低價與最新收盤比較。
/// </summary>
public static class WatchlistSignalCalculator
{
    /// <summary>取數值方向：正為 Up、負為 Down、零為 Flat、null（資料不足）為 Unknown。</summary>
    public static SignalTrend TrendOf(double? value)
        => value is not { } v ? SignalTrend.Unknown
         : v > 0 ? SignalTrend.Up
         : v < 0 ? SignalTrend.Down
         : SignalTrend.Flat;

    /// <summary>
    /// 判斷最新收盤是否接近 52 週還原高／低（門檻預設 ±5%）。收盤 ≥ 高 ×(1−門檻) 為近高；
    /// 收盤 ≤ 低 ×(1+門檻) 為近低；同時符合時以近高優先。收盤或高低無效時回 None。
    /// </summary>
    public static RangeSignal NearFiftyTwoWeek(
        double? latestClose, double? high52, double? low52, double threshold = 0.05)
    {
        if (latestClose is not { } close || close <= 0)
            return RangeSignal.None;
        if (high52 is { } high && high > 0 && close >= high * (1 - threshold))
            return RangeSignal.NearHigh;
        if (low52 is { } low && low > 0 && close <= low * (1 + threshold))
            return RangeSignal.NearLow;
        return RangeSignal.None;
    }
}
