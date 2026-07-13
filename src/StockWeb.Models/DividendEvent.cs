namespace StockWeb.Models;

/// <summary>
/// 除權息行事曆一筆事件（dividend_events）。InWatchlist 標記該檔是否在自選清單中，供月曆高亮。
/// </summary>
public record DividendEvent
{
    public string Code { get; init; } = "";
    public string? Name { get; init; }
    public string ExDate { get; init; } = "";
    public string? EventType { get; init; }
    public double? CashDividend { get; init; }
    public double? StockRatio { get; init; }
    public bool InWatchlist { get; init; }
}
