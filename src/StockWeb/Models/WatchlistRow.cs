namespace StockWeb.Models;

/// <summary>
/// 自選股清單一列（一次查詢組合）：最新收盤/漲跌%、近 5 日三大法人淨額合計、融資餘額變化。
/// 對應資料尚缺時各欄為 null（該檔可能停牌或當日尚無法人/融資資料）。
/// </summary>
public record WatchlistRow
{
    public string Code { get; init; } = "";
    public string? Name { get; init; }
    public double? Close { get; init; }
    public double? ChangePct { get; init; }
    /// <summary>近 5 個交易日（外資＋投信＋自營）淨買賣超股數合計。</summary>
    public double? InstitutionalNet5 { get; init; }
    /// <summary>最新交易日融資餘額較前日增減（margin_balance − margin_prev）。</summary>
    public double? MarginChange { get; init; }
}
