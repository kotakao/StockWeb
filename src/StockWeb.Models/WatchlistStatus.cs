namespace StockWeb.Models;

/// <summary>狀態訊號方向（近 5 日法人淨額、融資 5 日變化）。Unknown 表資料不足無法判斷。</summary>
public enum SignalTrend
{
    Unknown,
    Up,
    Down,
    Flat,
}

/// <summary>最新收盤相對 52 週還原高低的位置訊號（±5% 內）。</summary>
public enum RangeSignal
{
    None,
    NearHigh,
    NearLow,
}

/// <summary>
/// 觀察名單狀態板一列。行情欄（Close/ChangePct）恆有；持股欄（Shares 起）僅有持股者有值、純觀察為 null；
/// 狀態訊號欄一律有值（無資料時為 Unknown/None/false）。損益以未還原收盤與使用者填寫的平均成本計算。
/// </summary>
public record WatchlistStatusRow
{
    public string Code { get; init; } = "";
    public string? Name { get; init; }
    public double? Close { get; init; }
    public double? ChangePct { get; init; }

    // 持股與損益（未填持股時皆為 null）
    public double? Shares { get; init; }
    public double? AvgCost { get; init; }
    /// <summary>現值＝股數 × 收盤。</summary>
    public double? MarketValue { get; init; }
    /// <summary>未實現損益＝(收盤 − 平均成本) × 股數。</summary>
    public double? UnrealizedPnl { get; init; }
    /// <summary>報酬率(%)＝(收盤 − 平均成本) / 平均成本 × 100；零成本或無成本時為 null。</summary>
    public double? ReturnRate { get; init; }
    /// <summary>當日損益＝股數 × 當日漲跌額。</summary>
    public double? DayPnl { get; init; }

    // 狀態訊號（快速掃視，重用既有資料）
    public SignalTrend InstitutionalTrend { get; init; }
    public SignalTrend MarginTrend { get; init; }
    public RangeSignal RangePosition { get; init; }
    /// <summary>未來 14 日內有除權息事件。</summary>
    public bool HasDividendSoon { get; init; }
    /// <summary>未來 14 日內有法說會。</summary>
    public bool HasConferenceSoon { get; init; }
}

/// <summary>組合聚合列：總市值、總未實現損益、當日總損益（僅計有持股者）。</summary>
public record WatchlistStatusSummary(
    double TotalMarketValue,
    double TotalUnrealizedPnl,
    double TotalDayPnl);

/// <summary>GET /api/watchlist/status 回應：各列（預設按當日損益遞減）與聚合列。</summary>
public record WatchlistStatusResponse(
    IReadOnlyList<WatchlistStatusRow> Rows,
    WatchlistStatusSummary Summary);
