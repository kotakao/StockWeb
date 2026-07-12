using System.Text.Json.Serialization;

namespace StockWeb.Models;

/// <summary>
/// /api/screener 的請求條件（全部選填、以 AND 組合）。JSON 欄位為 snake_case，故各屬性標註對應名。
/// 估值三項取 valuation 最新日、revenue_yoy 取 monthly_revenue 最新月、連買日數取 institutional 連續淨買超日、
/// volume_multiple 為當日量對近 5 日均量倍數；market 為 TWSE/TPEX/ALL（省略視為 ALL）。
/// </summary>
public record ScreenerCriteria
{
    [JsonPropertyName("pe_max")] public double? PeMax { get; init; }
    [JsonPropertyName("dividend_yield_min")] public double? DividendYieldMin { get; init; }
    [JsonPropertyName("pb_max")] public double? PbMax { get; init; }
    [JsonPropertyName("revenue_yoy_min")] public double? RevenueYoyMin { get; init; }
    [JsonPropertyName("foreign_buy_days")] public int? ForeignBuyDays { get; init; }
    [JsonPropertyName("trust_buy_days")] public int? TrustBuyDays { get; init; }
    [JsonPropertyName("volume_multiple_min")] public double? VolumeMultipleMin { get; init; }
    [JsonPropertyName("market")] public string? Market { get; init; }
}
