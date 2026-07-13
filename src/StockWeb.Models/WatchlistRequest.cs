using System.Text.Json.Serialization;

namespace StockWeb.Models;

/// <summary>POST /api/watchlist 的請求本體：欲加入的股票代號。</summary>
public record WatchlistRequest
{
    [JsonPropertyName("code")] public string? Code { get; init; }
}
