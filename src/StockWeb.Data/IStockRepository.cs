using StockWeb.Models;
using StockWeb.Services;

namespace StockWeb.Data;

/// <summary>個股頁各端點的唯讀資料存取。全部序列回傳日期由舊到新。</summary>
public interface IStockRepository
{
    /// <summary>
    /// 近 days 日 OHLCV；adjusted 為 true 時回前復權序列；period 非 daily 時由日 K 聚合為週／月／年 K
    /// （先還原再聚合）。查無代號時 Quotes 為空、Name/Market 為 null。
    /// </summary>
    Task<StockQuotesResponse> GetQuotesAsync(string code, int days, bool adjusted, QuotePeriod period);

    Task<IReadOnlyList<StockInstitutionalRow>> GetInstitutionalAsync(string code, int days);

    Task<IReadOnlyList<StockMarginRow>> GetMarginAsync(string code, int days);

    Task<IReadOnlyList<StockValuationRow>> GetValuationAsync(string code, int days);

    Task<IReadOnlyList<StockRevenueRow>> GetRevenueAsync(string code, int months);

    Task<IReadOnlyList<DividendEvent>> GetDividendsAsync(string code);

    /// <summary>近 quarters 季損益（含讀取端計算的毛利率／營益率）。表不存在時回空集合。</summary>
    Task<IReadOnlyList<StockFinancialRow>> GetFinancialsAsync(string code, int quarters);

    /// <summary>取該代號最新日的公司名稱（供新聞查詢組字）；查無代號時回 null。</summary>
    Task<string?> GetNameAsync(string code);
}
