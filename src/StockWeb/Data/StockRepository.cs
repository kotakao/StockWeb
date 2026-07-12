using Dapper;
using Microsoft.Data.Sqlite;
using StockWeb.Models;
using StockWeb.Services;

namespace StockWeb.Data;

/// <summary>
/// 個股頁六端點的唯讀查詢。SQL 皆為固定字串、參數化 code/limit，不涉及任何寫入或 DDL（§3 鐵律）。
/// 各查詢以 date DESC LIMIT 取最新 N 筆後反轉為由舊到新。代號在台股跨市場不重複，故僅以 code 關聯。
/// dividend_events 與 monthly_revenue 自部署起才累積、可能尚未建立，故容忍表不存在時回空集合。
/// </summary>
public sealed class StockRepository : IStockRepository
{
    private readonly IDbConnectionFactory _connectionFactory;

    public StockRepository(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    private const string QuotesSql = """
        SELECT date AS Date, name AS Name, market AS Market,
               open AS Open, high AS High, low AS Low, close AS Close, volume AS Volume
        FROM daily_quotes
        WHERE code = @code
        ORDER BY date DESC
        LIMIT @limit
        """;

    private sealed record QuoteRow(
        string Date, string? Name, string? Market,
        double? Open, double? High, double? Low, double? Close, double? Volume);

    private sealed record EventRow(string ExDate, double? CashDividend, double? StockRatio);

    public async Task<StockQuotesResponse> GetQuotesAsync(string code, int days, bool adjusted)
    {
        using var connection = _connectionFactory.CreateReadOnly();

        // date DESC：rows[0] 為最新日。
        var rows = (await connection.QueryAsync<QuoteRow>(QuotesSql, new { code, limit = days })).ToList();
        if (rows.Count == 0)
            return new StockQuotesResponse(code, null, null, adjusted, Array.Empty<StockQuote>());

        var name = rows[0].Name;   // 以最新日的名稱／市場為準
        var market = rows[0].Market;

        var quotes = rows
            .Select(r => new StockQuote(r.Date, r.Open, r.High, r.Low, r.Close, r.Volume))
            .ToList();

        if (adjusted)
        {
            var newest = rows[0].Date;              // DESC → 第一列最新
            var oldest = rows[^1].Date;             // 最後一列最舊
            var events = await GetAdjustmentsAsync(connection, code, oldest, newest);
            if (events.Count > 0)
                quotes = AdjustedPriceService.Adjust(quotes, events).ToList();
        }

        quotes.Reverse();   // 反轉為由舊到新
        return new StockQuotesResponse(code, name, market, adjusted, quotes);
    }

    // 僅取落在資料序列日期範圍內 [oldest, newest] 的事件（非未來日）；表不存在時回空（不還原）。
    private static async Task<IReadOnlyList<DividendAdjustment>> GetAdjustmentsAsync(
        SqliteConnection connection, string code, string oldest, string newest)
    {
        if (!await TableExistsAsync(connection, "dividend_events"))
            return Array.Empty<DividendAdjustment>();

        // 選宣告型別為 REAL 的原欄位（勿用 COALESCE，否則空結果集時欄位無宣告型別、Dapper 無法對映）；
        // 現金股利／配股率的 NULL 於 C# 端補 0。
        const string sql = """
            SELECT ex_date AS ExDate, cash_dividend AS CashDividend, stock_ratio AS StockRatio
            FROM dividend_events
            WHERE code = @code AND ex_date >= @oldest AND ex_date <= @newest
            """;
        var rows = await connection.QueryAsync<EventRow>(sql, new { code, oldest, newest });
        return rows.Select(r => new DividendAdjustment(r.ExDate, r.CashDividend ?? 0, r.StockRatio ?? 0)).ToList();
    }

    private const string InstitutionalSql = """
        SELECT date AS Date, foreign_net AS ForeignNet, trust_net AS TrustNet, dealer_net AS DealerNet
        FROM institutional
        WHERE code = @code
        ORDER BY date DESC
        LIMIT @limit
        """;

    public async Task<IReadOnlyList<StockInstitutionalRow>> GetInstitutionalAsync(string code, int days)
        => await QueryAscendingAsync<StockInstitutionalRow>(InstitutionalSql, code, days);

    private const string MarginSql = """
        SELECT date AS Date, margin_balance AS MarginBalance, short_balance AS ShortBalance
        FROM margin
        WHERE code = @code
        ORDER BY date DESC
        LIMIT @limit
        """;

    public async Task<IReadOnlyList<StockMarginRow>> GetMarginAsync(string code, int days)
        => await QueryAscendingAsync<StockMarginRow>(MarginSql, code, days);

    private const string ValuationSql = """
        SELECT date AS Date, pe AS Pe, dividend_yield AS DividendYield, pb AS Pb
        FROM valuation
        WHERE code = @code
        ORDER BY date DESC
        LIMIT @limit
        """;

    public async Task<IReadOnlyList<StockValuationRow>> GetValuationAsync(string code, int days)
        => await QueryAscendingAsync<StockValuationRow>(ValuationSql, code, days);

    private const string RevenueSql = """
        SELECT year_month AS YearMonth, revenue AS Revenue, yoy_pct AS YoyPct
        FROM monthly_revenue
        WHERE code = @code
        ORDER BY year_month DESC
        LIMIT @limit
        """;

    public async Task<IReadOnlyList<StockRevenueRow>> GetRevenueAsync(string code, int months)
    {
        using var connection = _connectionFactory.CreateReadOnly();
        if (!await TableExistsAsync(connection, "monthly_revenue"))
            return Array.Empty<StockRevenueRow>();

        var rows = (await connection.QueryAsync<StockRevenueRow>(RevenueSql, new { code, limit = months })).ToList();
        rows.Reverse();
        return rows;
    }

    private const string DividendsSql = """
        SELECT code AS Code, name AS Name, ex_date AS ExDate, event_type AS EventType,
               cash_dividend AS CashDividend, stock_ratio AS StockRatio
        FROM dividend_events
        WHERE code = @code
        ORDER BY ex_date
        """;

    public async Task<IReadOnlyList<DividendEvent>> GetDividendsAsync(string code)
    {
        using var connection = _connectionFactory.CreateReadOnly();
        if (!await TableExistsAsync(connection, "dividend_events"))
            return Array.Empty<DividendEvent>();

        var rows = await connection.QueryAsync<DividendEvent>(DividendsSql, new { code });
        return rows.ToList();
    }

    // date DESC LIMIT 取最新 N 筆後反轉為由舊到新，供圖表時間軸使用。
    private async Task<IReadOnlyList<T>> QueryAscendingAsync<T>(string sql, string code, int limit)
    {
        using var connection = _connectionFactory.CreateReadOnly();
        var rows = (await connection.QueryAsync<T>(sql, new { code, limit })).ToList();
        rows.Reverse();
        return rows;
    }

    private static async Task<bool> TableExistsAsync(SqliteConnection connection, string table)
    {
        var name = await connection.ExecuteScalarAsync<string?>(
            "SELECT name FROM sqlite_master WHERE type='table' AND name=@table", new { table });
        return name is not null;
    }
}
