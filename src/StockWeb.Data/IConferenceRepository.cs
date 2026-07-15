using StockWeb.Models;

namespace StockWeb.Data;

/// <summary>法說會查詢（investor_conferences，唯讀）。schema 由 Python 端獨占管理，表不存在時回空集合。</summary>
public interface IConferenceRepository
{
    /// <summary>單一個股近 limit 場法說會（依召開日 fact_date 由近到遠）。</summary>
    Task<IReadOnlyList<Conference>> GetByCodeAsync(string code, int limit);

    /// <summary>指定月份（該月第一天）內、依召開日 fact_date 的全市場法說會，含自選股高亮旗標。</summary>
    Task<IReadOnlyList<Conference>> GetByMonthAsync(DateOnly monthStart);
}
