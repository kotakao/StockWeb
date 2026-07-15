using StockWeb.Models;

namespace StockWeb.Services;

/// <summary>
/// 持股損益計算（純函數，不依賴 ASP.NET 型別，可單元測試）。
/// 一律以「未還原收盤」與使用者填寫的平均成本計算——平均成本是名目取得成本，不因除權息調整；
/// 還原價僅用於 52 週高低訊號（見 <see cref="WatchlistSignalCalculator"/>）。
/// 任一必要輸入為 null（未填持股）時對應結果為 null，交由呼叫端顯示「—」。
/// </summary>
public static class HoldingsCalculator
{
    /// <summary>現值＝股數 × 收盤。</summary>
    public static double? MarketValue(double? shares, double? close)
        => shares is { } s && close is { } c ? s * c : null;

    /// <summary>未實現損益＝(收盤 − 平均成本) × 股數。</summary>
    public static double? UnrealizedPnl(double? shares, double? avgCost, double? close)
        => shares is { } s && avgCost is { } a && close is { } c ? (c - a) * s : null;

    /// <summary>報酬率(%)＝(收盤 − 平均成本) / 平均成本 × 100。零成本或無成本時無法計算，回 null。</summary>
    public static double? ReturnRate(double? avgCost, double? close)
        => avgCost is { } a && a > 0 && close is { } c ? (c - a) / a * 100 : null;

    /// <summary>當日損益＝股數 × 當日漲跌額（daily_quotes.change）。</summary>
    public static double? DayPnl(double? shares, double? change)
        => shares is { } s && change is { } ch ? s * ch : null;

    /// <summary>聚合各列的市值／未實現損益／當日損益（無持股者各欄視為 0）。</summary>
    public static WatchlistStatusSummary Summarize(IEnumerable<WatchlistStatusRow> rows)
    {
        double marketValue = 0, unrealized = 0, dayPnl = 0;
        foreach (var row in rows)
        {
            marketValue += row.MarketValue ?? 0;
            unrealized += row.UnrealizedPnl ?? 0;
            dayPnl += row.DayPnl ?? 0;
        }
        return new WatchlistStatusSummary(marketValue, unrealized, dayPnl);
    }
}
