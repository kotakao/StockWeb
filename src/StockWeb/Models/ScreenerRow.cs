namespace StockWeb.Models;

/// <summary>
/// 篩選結果一列：代號/名稱/收盤/漲跌與漲跌%，加上各啟用條件的實際觸發值。
/// 未啟用的條件其對應欄位為 null（該欄不在 SELECT 之中，Dapper 留預設）。
/// </summary>
public record ScreenerRow
{
    public string Code { get; init; } = "";
    public string? Name { get; init; }
    public double? Close { get; init; }
    public double? Change { get; init; }
    public double? ChangePct { get; init; }
    public double? Pe { get; init; }
    public double? DividendYield { get; init; }
    public double? Pb { get; init; }
    public double? RevenueYoy { get; init; }
    public int? ForeignBuyDays { get; init; }
    public int? TrustBuyDays { get; init; }
    public double? VolumeMultiple { get; init; }
}
