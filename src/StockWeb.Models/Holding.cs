using System.Text.Json.Serialization;

namespace StockWeb.Models;

/// <summary>
/// 持股一筆（holdings 表，網頁端 user_id 固定 "0"）。股數／平均成本可為 null（尚未填寫視為純觀察）。
/// UpdatedAt 為 Bot 慣例的 ISO 秒精度字串（yyyy-MM-ddTHH:mm:ss）。
/// </summary>
public record Holding
{
    public string Code { get; init; } = "";
    public double? Shares { get; init; }
    public double? AvgCost { get; init; }
    public string? UpdatedAt { get; init; }
}

/// <summary>PUT /api/holdings 請求本體：代號、股數、平均成本（新增或覆蓋，upsert）。</summary>
public record HoldingRequest
{
    [JsonPropertyName("code")] public string? Code { get; init; }
    [JsonPropertyName("shares")] public double? Shares { get; init; }
    [JsonPropertyName("avgCost")] public double? AvgCost { get; init; }
}
