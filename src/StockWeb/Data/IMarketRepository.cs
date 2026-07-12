using StockWeb.Models;

namespace StockWeb.Data;

/// <summary>大盤日線序列查詢（market_daily）。</summary>
public interface IMarketRepository
{
    /// <summary>取近 days 個交易日的 TWSE 大盤資料，依日期新到舊（比照 get_market_daily）。</summary>
    Task<IReadOnlyList<MarketDailyRow>> GetDailyAsync(int days);
}
