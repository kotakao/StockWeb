namespace StockWeb.Models;

/// <summary>
/// market_daily 一列（大盤層級）。欄位對齊 storage.py 的 _MARKET_DAILY_COLUMNS，
/// 皆唯讀取得；數值欄可為 null（該日缺該指標）。供 /api/market/daily 與寬度計算輸入。
/// </summary>
public record MarketDailyRow
{
    public string Date { get; init; } = "";
    public double? IndexClose { get; init; }
    public double? IndexChangePct { get; init; }
    public double? Turnover { get; init; }
    public double? UpCount { get; init; }
    public double? DownCount { get; init; }
    public double? ForeignNet { get; init; }
    public double? TrustNet { get; init; }
    public double? DealerNet { get; init; }
    public double? MarginBalance { get; init; }
    public double? ShortBalance { get; init; }
}
