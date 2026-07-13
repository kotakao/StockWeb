using StockWeb.Models;

namespace StockWeb.Data;

/// <summary>除權息行事曆查詢（唯讀）。</summary>
public interface ICalendarRepository
{
    /// <summary>取得指定月份（該月第一天）內的除權息事件，含自選股高亮旗標。</summary>
    Task<IReadOnlyList<DividendEvent>> GetDividendsAsync(DateOnly monthStart);
}
