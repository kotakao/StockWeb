using StockWeb.Models;

namespace StockWeb.Services;

/// <summary>
/// 前復權（還原價）計算，移植自 StockDCbot analysis.adjust_history（§7，數字以 Python 版為準）。
/// 由最新事件往回逐事件調整：對 ex_date「之前」（date &lt; ex_date）的每列套用
/// 調整後價 = (原價 - 現金股利) / (1 + 配股率)；事件當日與其後不動。OHLC 四價同步調整、量不調。
/// 純函數、不依賴 ASP.NET 型別，可單元測試。呼叫端負責先濾掉未來日／序列範圍外的事件。
/// </summary>
public static class AdjustedPriceService
{
    /// <summary>
    /// 對 history 套用 events 的前復權。events 依 ex_date 由新到舊逐一施加（與 Python 排序一致），
    /// 每次施加會累積作用於前次已調整的值。回傳新序列，不修改輸入。
    /// </summary>
    public static IReadOnlyList<StockQuote> Adjust(
        IReadOnlyList<StockQuote> history,
        IEnumerable<DividendAdjustment> events)
    {
        var adjusted = history.ToList();

        foreach (var ev in events.OrderByDescending(e => e.ExDate, StringComparer.Ordinal))
        {
            var cash = ev.CashDividend;
            var divisor = 1.0 + ev.StockRatio;
            for (var i = 0; i < adjusted.Count; i++)
            {
                var row = adjusted[i];
                // 事件當日與其後（row.Date >= ex_date）不動，只調整之前的歷史價。
                if (string.CompareOrdinal(row.Date, ev.ExDate) >= 0)
                    continue;

                adjusted[i] = row with
                {
                    Open = AdjustPrice(row.Open, cash, divisor),
                    High = AdjustPrice(row.High, cash, divisor),
                    Low = AdjustPrice(row.Low, cash, divisor),
                    Close = AdjustPrice(row.Close, cash, divisor),
                };
            }
        }

        return adjusted;
    }

    private static double? AdjustPrice(double? value, double cash, double divisor)
        => value is { } v ? (v - cash) / divisor : null;
}
