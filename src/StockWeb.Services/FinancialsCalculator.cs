namespace StockWeb.Services;

/// <summary>
/// 季度損益比率計算（純邏輯，可單元測試）。毛利率／營益率由讀取端以營收計算，不存冗餘欄位（DC-J 契約）。
/// 分母（營收）缺漏或為 0 時回 null，避免除以零；比率以百分比（%）表示。
/// </summary>
public static class FinancialsCalculator
{
    /// <summary>毛利率% = 營業毛利 ÷ 營業收入 × 100。</summary>
    public static double? GrossMargin(double? revenue, double? grossProfit)
        => Percent(grossProfit, revenue);

    /// <summary>營業利益率% = 營業利益 ÷ 營業收入 × 100。</summary>
    public static double? OperatingMargin(double? revenue, double? operatingIncome)
        => Percent(operatingIncome, revenue);

    private static double? Percent(double? numerator, double? denominator)
        => numerator is { } n && denominator is { } d && d != 0
            ? n / d * 100.0
            : null;
}
