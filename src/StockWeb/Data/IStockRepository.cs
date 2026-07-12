using StockWeb.Models;

namespace StockWeb.Data;

/// <summary>個股頁六端點的唯讀資料存取。全部序列回傳日期由舊到新。</summary>
public interface IStockRepository
{
    /// <summary>近 days 日 OHLCV；adjusted 為 true 時回前復權序列。查無代號時 Quotes 為空、Name/Market 為 null。</summary>
    Task<StockQuotesResponse> GetQuotesAsync(string code, int days, bool adjusted);

    Task<IReadOnlyList<StockInstitutionalRow>> GetInstitutionalAsync(string code, int days);

    Task<IReadOnlyList<StockMarginRow>> GetMarginAsync(string code, int days);

    Task<IReadOnlyList<StockValuationRow>> GetValuationAsync(string code, int days);

    Task<IReadOnlyList<StockRevenueRow>> GetRevenueAsync(string code, int months);

    Task<IReadOnlyList<DividendEvent>> GetDividendsAsync(string code);
}
