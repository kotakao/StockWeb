using System.Globalization;
using StockWeb.Models;

namespace StockWeb.Services;

/// <summary>K 線週期。日 K 為原始，週／月／年 K 由日 K 於伺服器端聚合。</summary>
public enum QuotePeriod
{
    Daily,
    Weekly,
    Monthly,
    Yearly,
}

/// <summary>
/// 由日 K 聚合為週／月／年 K 的純邏輯（不依賴 ASP.NET 型別，可單元測試）。
/// 開＝區間首日開、收＝末日收、高＝區間最高、低＝區間最低、量＝加總；
/// 週以 ISO 週分組、月以日曆月、年以日曆年。每根以「區間內最後交易日」為時間。
/// 前復權須由呼叫端先套用再聚合（先還原再聚合），本類別不涉及還原。
/// 輸入須為同一代號、由舊到新排序的日 K；殘缺區間（不足一根的殘週）如實聚合為一根。
/// </summary>
public static class QuoteAggregator
{
    public static IReadOnlyList<StockQuote> Aggregate(IReadOnlyList<StockQuote> daily, QuotePeriod period)
    {
        if (period == QuotePeriod.Daily || daily.Count == 0)
            return daily;

        // 輸入由舊到新 → GroupBy 依鍵首次出現順序保留，聚合結果亦由舊到新。
        return daily
            .GroupBy(q => GroupKey(ParseDate(q.Date), period))
            .Select(BuildBar)
            .ToList();
    }

    private static StockQuote BuildBar(IEnumerable<StockQuote> group)
    {
        var rows = group.ToList();   // 組內維持由舊到新
        return new StockQuote(
            Date: rows[^1].Date,           // 以區間內最後交易日為該根 K 的時間
            Open: rows[0].Open,            // 首日開
            High: Max(rows, r => r.High),  // 區間最高
            Low: Min(rows, r => r.Low),    // 區間最低
            Close: rows[^1].Close,         // 末日收
            Volume: Sum(rows, r => r.Volume));
    }

    private static string GroupKey(DateTime d, QuotePeriod period) => period switch
    {
        // ISO 週年＋週序，正確處理跨年週（例如 1/1 可能屬前一年第 52/53 週）。
        QuotePeriod.Weekly => $"{ISOWeek.GetYear(d):D4}-W{ISOWeek.GetWeekOfYear(d):D2}",
        QuotePeriod.Monthly => d.ToString("yyyy-MM", CultureInfo.InvariantCulture),
        QuotePeriod.Yearly => d.ToString("yyyy", CultureInfo.InvariantCulture),
        _ => d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
    };

    private static DateTime ParseDate(string date)
        => DateTime.ParseExact(date, "yyyy-MM-dd", CultureInfo.InvariantCulture);

    private static double? Max(List<StockQuote> rows, Func<StockQuote, double?> selector)
        => Reduce(rows, selector, Enumerable.Max);

    private static double? Min(List<StockQuote> rows, Func<StockQuote, double?> selector)
        => Reduce(rows, selector, Enumerable.Min);

    private static double? Sum(List<StockQuote> rows, Func<StockQuote, double?> selector)
    {
        var values = Values(rows, selector);
        return values.Count > 0 ? values.Sum() : null;   // 全缺回 null（避免把「無量」呈現為 0）
    }

    private static double? Reduce(List<StockQuote> rows, Func<StockQuote, double?> selector,
        Func<IEnumerable<double>, double> reducer)
    {
        var values = Values(rows, selector);
        return values.Count > 0 ? reducer(values) : null;
    }

    private static List<double> Values(List<StockQuote> rows, Func<StockQuote, double?> selector)
        => rows.Select(selector).Where(v => v is not null).Select(v => v!.Value).ToList();
}
