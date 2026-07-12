using StockWeb.Models;

namespace StockWeb.Data;

/// <summary>條件選股查詢（唯讀）。</summary>
public interface IScreenerRepository
{
    /// <summary>依條件回傳符合的股票列（上限 200 列），含各啟用條件的實際觸發值。</summary>
    Task<IReadOnlyList<ScreenerRow>> ScreenAsync(ScreenerCriteria criteria);
}
