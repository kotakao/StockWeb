using System.Text;
using StockWeb.Models;

namespace StockWeb.Services;

/// <summary>Builder 產出：待執行 SQL 與其參數字典（供 Dapper 帶入；使用者輸入一律走參數，不入 SQL 字串）。</summary>
public sealed record ScreenerQuery(string Sql, IReadOnlyDictionary<string, object?> Parameters);

/// <summary>
/// 篩選 SQL 組譯（純字串組裝，可單元測試、不依賴 ASP.NET）。設計要點：
/// 1. 使用者輸入全部走 @參數，SQL 骨架為固定字串，market 只在 {TWSE,TPEX,ALL} 內取值，杜絕字串拼接注入。
/// 2. 兩段式：內層先以「最新日」把候選集縮到當日全市場，外層才對縮小後結果套條件（估值/月營收以 JOIN 取值、
///    連買日數與量能倍數以相關子查詢逐檔計算），避免對全表跑昂貴的連續日運算。
/// 3. 連續淨買超 N 日 = 由最新日回推、net &gt; 0 且晚於「最近一次中斷日」（net ≤ 0 或 NULL）的天數；
///    NULL 視為中斷，恰好 N 日即成立。
/// </summary>
public static class ScreenerQueryBuilder
{
    public const int MaxRows = 200;
    public const int VolumeLookback = 5; // 量能倍數基準：近 5 日均量（不含當日）

    public static ScreenerQuery Build(ScreenerCriteria criteria)
    {
        var market = NormalizeMarket(criteria.Market);
        var byMarket = market != "ALL";

        var parameters = new Dictionary<string, object?>();
        if (byMarket)
            parameters["market"] = market;

        // 各表 market 過濾片段：ALL 時為空字串，否則對該表自身 market 欄套 @market。
        string MktAnd(string alias) => byMarket ? $" AND {alias}.market = @market" : "";

        // 各表「最新日/月」子查詢，皆與主查詢同一 market scope。
        string quoteDate = byMarket
            ? "(SELECT MAX(date) FROM daily_quotes WHERE market = @market)"
            : "(SELECT MAX(date) FROM daily_quotes)";
        string valDate = byMarket
            ? "(SELECT MAX(date) FROM valuation WHERE market = @market)"
            : "(SELECT MAX(date) FROM valuation)";
        string revMonth = byMarket
            ? "(SELECT MAX(year_month) FROM monthly_revenue WHERE market = @market)"
            : "(SELECT MAX(year_month) FROM monthly_revenue)";

        // 內層固定欄位：代號、名稱、收盤、漲跌，漲跌% 由 change/前收 換算（前收 = close - change，除零則 NULL）。
        var innerColumns = new List<string>
        {
            "q.code AS Code",
            "q.name AS Name",
            "q.close AS Close",
            "q.change AS Change",
            "CASE WHEN (q.close - q.change) <> 0 THEN ROUND(q.change / (q.close - q.change) * 100, 2) END AS ChangePct",
        };
        var joins = new List<string>();
        var outerPredicates = new List<string>();
        var joinValuation = false;
        var joinRevenue = false;

        if (criteria.PeMax is { } peMax)
        {
            joinValuation = true;
            innerColumns.Add("v.pe AS Pe");
            outerPredicates.Add("t.Pe <= @pe_max");
            parameters["pe_max"] = peMax;
        }
        if (criteria.DividendYieldMin is { } dyMin)
        {
            joinValuation = true;
            innerColumns.Add("v.dividend_yield AS DividendYield");
            outerPredicates.Add("t.DividendYield >= @dividend_yield_min");
            parameters["dividend_yield_min"] = dyMin;
        }
        if (criteria.PbMax is { } pbMax)
        {
            joinValuation = true;
            innerColumns.Add("v.pb AS Pb");
            outerPredicates.Add("t.Pb <= @pb_max");
            parameters["pb_max"] = pbMax;
        }
        if (criteria.RevenueYoyMin is { } yoyMin)
        {
            joinRevenue = true;
            innerColumns.Add("r.yoy_pct AS RevenueYoy");
            outerPredicates.Add("t.RevenueYoy >= @revenue_yoy_min");
            parameters["revenue_yoy_min"] = yoyMin;
        }
        if (criteria.ForeignBuyDays is { } fDays)
        {
            innerColumns.Add(StreakColumn("foreign_net", "ForeignBuyDays", MktAnd));
            outerPredicates.Add("t.ForeignBuyDays >= @foreign_buy_days");
            parameters["foreign_buy_days"] = fDays;
        }
        if (criteria.TrustBuyDays is { } tDays)
        {
            innerColumns.Add(StreakColumn("trust_net", "TrustBuyDays", MktAnd));
            outerPredicates.Add("t.TrustBuyDays >= @trust_buy_days");
            parameters["trust_buy_days"] = tDays;
        }
        if (criteria.VolumeMultipleMin is { } volMin)
        {
            innerColumns.Add(VolumeMultipleColumn(quoteDate, MktAnd));
            outerPredicates.Add("t.VolumeMultiple >= @volume_multiple_min");
            parameters["volume_multiple_min"] = volMin;
        }

        if (joinValuation)
            joins.Add($"LEFT JOIN valuation v ON v.market = q.market AND v.code = q.code AND v.date = {valDate}");
        if (joinRevenue)
            joins.Add($"LEFT JOIN monthly_revenue r ON r.market = q.market AND r.code = q.code AND r.year_month = {revMonth}");

        var sql = new StringBuilder();
        sql.Append("SELECT * FROM (\n");
        sql.Append("    SELECT ").Append(string.Join(", ", innerColumns)).Append('\n');
        sql.Append("    FROM daily_quotes q\n");
        foreach (var join in joins)
            sql.Append("    ").Append(join).Append('\n');
        sql.Append("    WHERE q.date = ").Append(quoteDate).Append(MktAnd("q")).Append('\n');
        sql.Append(") t\n");
        if (outerPredicates.Count > 0)
            sql.Append("WHERE ").Append(string.Join(" AND ", outerPredicates)).Append('\n');
        sql.Append("ORDER BY t.Code\n");
        sql.Append("LIMIT ").Append(MaxRows);

        return new ScreenerQuery(sql.ToString(), parameters);
    }

    /// <summary>連續淨買超日數：net &gt; 0 且晚於最近一次中斷日（net ≤ 0 或 NULL）的天數，由最新日回推。</summary>
    private static string StreakColumn(string netColumn, string alias, Func<string, string> mktAnd) =>
        $"(SELECT COUNT(*) FROM institutional i " +
        $"WHERE i.code = q.code{mktAnd("i")} AND i.{netColumn} > 0 " +
        $"AND i.date > COALESCE((SELECT MAX(i2.date) FROM institutional i2 " +
        $"WHERE i2.code = q.code{mktAnd("i2")} AND (i2.{netColumn} <= 0 OR i2.{netColumn} IS NULL)), '')) AS {alias}";

    /// <summary>量能倍數：當日量 ÷ 近 5 日均量（不含當日）。均量為 NULL/0 時以 NULLIF 保護，結果 NULL 會被外層條件濾除。</summary>
    private static string VolumeMultipleColumn(string quoteDate, Func<string, string> mktAnd) =>
        $"ROUND(q.volume / NULLIF((SELECT AVG(volume) FROM (" +
        $"SELECT d.volume FROM daily_quotes d " +
        $"WHERE d.code = q.code{mktAnd("d")} AND d.date < {quoteDate} AND d.volume IS NOT NULL " +
        $"ORDER BY d.date DESC LIMIT {VolumeLookback})), 0), 4) AS VolumeMultiple";

    /// <summary>market 正規化：null/空白視為 ALL，其餘轉大寫（合法性由 API 層驗證）。</summary>
    public static string NormalizeMarket(string? market) =>
        string.IsNullOrWhiteSpace(market) ? "ALL" : market.Trim().ToUpperInvariant();
}
