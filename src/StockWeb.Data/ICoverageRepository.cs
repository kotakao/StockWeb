using StockWeb.Models;

namespace StockWeb.Data;

/// <summary>各資料表的覆蓋範圍查詢（起訖日、去重日數）。</summary>
public interface ICoverageRepository
{
    Task<IReadOnlyList<CoverageRow>> GetCoverageAsync();
}
