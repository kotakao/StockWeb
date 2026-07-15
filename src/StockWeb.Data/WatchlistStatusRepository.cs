using Dapper;
using Microsoft.Data.Sqlite;
using StockWeb.Models;
using StockWeb.Services;

namespace StockWeb.Data;

/// <summary>
/// 觀察名單狀態板查詢。以固定次數的批次查詢（與清單長度無關）組合整份清單，避免逐檔 N+1：
///   A 行情＋持股＋近 5 日法人淨額＋融資 5 日變化（單一語句）；
///   B/C 未來 14 日除權息／法說會的代號集合（表可能未建立時容忍不存在）；
///   D/E 一年日 K 與除權息批次撈回，記憶體內分組還原後算 52 週高低。
/// 全程唯讀。損益／訊號計算沿用 Services 純函數（讀取端計算，比照 StockRepository 慣例）。
/// </summary>
public sealed class WatchlistStatusRepository : IWatchlistStatusRepository
{
    private readonly IDbConnectionFactory _connectionFactory;

    public WatchlistStatusRepository(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    // A：主行情＋持股＋近 5 日法人淨額合計＋融資 5 日變化（最新 − 第 6 新，不足 6 筆則為 NULL→Unknown）。
    // 代號在台股跨市場不重複，故僅以 code 關聯（不帶 market）。
    private const string ScalarSql = """
        SELECT
            w.code AS Code,
            q.name AS Name,
            q.close AS Close,
            q.change AS Change,
            CASE WHEN (q.close - q.change) <> 0
                 THEN ROUND(q.change / (q.close - q.change) * 100, 2) END AS ChangePct,
            h.shares AS Shares,
            h.avg_cost AS AvgCost,
            (SELECT SUM(COALESCE(i.foreign_net, 0) + COALESCE(i.trust_net, 0) + COALESCE(i.dealer_net, 0))
               FROM (SELECT foreign_net, trust_net, dealer_net FROM institutional
                     WHERE code = w.code ORDER BY date DESC LIMIT 5) i) AS InstitutionalNet5,
            ((SELECT margin_balance FROM margin WHERE code = w.code ORDER BY date DESC LIMIT 1)
             - (SELECT margin_balance FROM margin WHERE code = w.code ORDER BY date DESC LIMIT 1 OFFSET 5)) AS MarginChange5
        FROM watchlist w
        LEFT JOIN holdings h ON h.user_id = w.user_id AND h.code = w.code
        LEFT JOIN daily_quotes q
            ON q.code = w.code AND q.date = (SELECT MAX(date) FROM daily_quotes WHERE code = w.code)
        WHERE w.user_id = @uid
        ORDER BY w.code
        """;

    // 以 init 屬性（非位置建構子）承接：ChangePct/InstitutionalNet5/MarginChange5 為計算欄位、無宣告型別，
    // 空結果或 NULL 時 Dapper 會判為 byte[]，位置建構子將無法對映（比照 StockRepository 的相同陷阱）。
    private sealed record ScalarRow
    {
        public string Code { get; init; } = "";
        public string? Name { get; init; }
        public double? Close { get; init; }
        public double? Change { get; init; }
        public double? ChangePct { get; init; }
        public double? Shares { get; init; }
        public double? AvgCost { get; init; }
        public double? InstitutionalNet5 { get; init; }
        public double? MarginChange5 { get; init; }
    }

    public async Task<WatchlistStatusResponse> GetStatusAsync()
    {
        var uid = WatchlistRepository.WebUserId;
        using var connection = _connectionFactory.CreateReadOnly();

        var scalars = (await connection.QueryAsync<ScalarRow>(ScalarSql, new { uid })).ToList();
        if (scalars.Count == 0)
            return new WatchlistStatusResponse(Array.Empty<WatchlistStatusRow>(), new WatchlistStatusSummary(0, 0, 0));

        var today = DateTime.Today;
        var todayStr = today.ToString("yyyy-MM-dd");
        var soonStr = today.AddDays(14).ToString("yyyy-MM-dd");
        var yearAgoStr = today.AddYears(-1).ToString("yyyy-MM-dd");

        // B/C：未來 14 日（含今日）內有事件的代號集合。表未建立時回空集合（比照既有 repo）。
        var dividendSoon = await CodesWithEventAsync(connection, "dividend_events", "ex_date", uid, todayStr, soonStr);
        var conferenceSoon = await CodesWithEventAsync(connection, "investor_conferences", "fact_date", uid, todayStr, soonStr);

        // D/E：52 週還原高低。
        var rangeByCode = await ComputeFiftyTwoWeekAsync(connection, uid, yearAgoStr);

        var rows = scalars
            .Select(s => BuildRow(s, dividendSoon, conferenceSoon, rangeByCode))
            .OrderByDescending(r => r.DayPnl ?? double.MinValue)   // 預設按當日損益遞減，無持股（null）殿後
            .ThenBy(r => r.Code, StringComparer.Ordinal)
            .ToList();

        return new WatchlistStatusResponse(rows, HoldingsCalculator.Summarize(rows));
    }

    private static WatchlistStatusRow BuildRow(
        ScalarRow s, IReadOnlySet<string> dividendSoon, IReadOnlySet<string> conferenceSoon,
        IReadOnlyDictionary<string, RangeSignal> rangeByCode)
        => new()
        {
            Code = s.Code,
            Name = s.Name,
            Close = s.Close,
            ChangePct = s.ChangePct,
            Shares = s.Shares,
            AvgCost = s.AvgCost,
            MarketValue = HoldingsCalculator.MarketValue(s.Shares, s.Close),
            UnrealizedPnl = HoldingsCalculator.UnrealizedPnl(s.Shares, s.AvgCost, s.Close),
            ReturnRate = HoldingsCalculator.ReturnRate(s.AvgCost, s.Close),
            DayPnl = HoldingsCalculator.DayPnl(s.Shares, s.Change),
            InstitutionalTrend = WatchlistSignalCalculator.TrendOf(s.InstitutionalNet5),
            MarginTrend = WatchlistSignalCalculator.TrendOf(s.MarginChange5),
            RangePosition = rangeByCode.TryGetValue(s.Code, out var r) ? r : RangeSignal.None,
            HasDividendSoon = dividendSoon.Contains(s.Code),
            HasConferenceSoon = conferenceSoon.Contains(s.Code),
        };

    // 表名與日期欄名為此類別內固定字面常數（非使用者輸入），值一律參數化——不違反「嚴禁字串拼接使用者輸入」。
    private static async Task<IReadOnlySet<string>> CodesWithEventAsync(
        SqliteConnection connection, string table, string dateColumn, string uid, string from, string to)
    {
        if (!await TableExistsAsync(connection, table))
            return new HashSet<string>();

        var sql = $"""
            SELECT DISTINCT code FROM {table}
            WHERE {dateColumn} >= @from AND {dateColumn} <= @to
              AND code IN (SELECT code FROM watchlist WHERE user_id = @uid)
            """;
        var codes = await connection.QueryAsync<string>(sql, new { from, to, uid });
        return codes.ToHashSet();
    }

    private sealed record WindowRow(string Code, string Date, double? High, double? Low, double? Close);

    private static async Task<IReadOnlyDictionary<string, RangeSignal>> ComputeFiftyTwoWeekAsync(
        SqliteConnection connection, string uid, string yearAgo)
    {
        // 一次撈回所有自選代號近一年日 K，記憶體內分組（不逐檔查詢）。
        const string quoteSql = """
            SELECT code AS Code, date AS Date, high AS High, low AS Low, close AS Close
            FROM daily_quotes
            WHERE date >= @yearAgo AND code IN (SELECT code FROM watchlist WHERE user_id = @uid)
            ORDER BY code, date
            """;
        var quotesByCode = (await connection.QueryAsync<WindowRow>(quoteSql, new { yearAgo, uid }))
            .GroupBy(r => r.Code)
            .ToDictionary(g => g.Key, g => g.OrderBy(r => r.Date, StringComparer.Ordinal).ToList());

        // 除權息事件（表可能未建立）：一次撈回，供還原。
        var eventsByCode = new Dictionary<string, List<DividendAdjustment>>();
        if (await TableExistsAsync(connection, "dividend_events"))
        {
            const string eventSql = """
                SELECT code AS Code, ex_date AS ExDate, cash_dividend AS CashDividend, stock_ratio AS StockRatio
                FROM dividend_events
                WHERE ex_date >= @yearAgo AND code IN (SELECT code FROM watchlist WHERE user_id = @uid)
                """;
            var events = await connection.QueryAsync<(string Code, string ExDate, double? Cash, double? Ratio)>(
                eventSql, new { yearAgo, uid });
            eventsByCode = events
                .GroupBy(e => e.Code)
                .ToDictionary(g => g.Key,
                    g => g.Select(e => new DividendAdjustment(e.ExDate, e.Cash ?? 0, e.Ratio ?? 0)).ToList());
        }

        var result = new Dictionary<string, RangeSignal>();
        foreach (var (code, series) in quotesByCode)
        {
            if (series.Count == 0)
                continue;

            var quotes = (IReadOnlyList<StockQuote>)series
                .Select(r => new StockQuote(r.Date, null, r.High, r.Low, r.Close, null))
                .ToList();

            // 只套用落在序列日期範圍內的事件（非未來日），比照 StockRepository。
            if (eventsByCode.TryGetValue(code, out var events))
            {
                var oldest = series[0].Date;
                var newest = series[^1].Date;
                var applicable = events.Where(e =>
                    string.CompareOrdinal(e.ExDate, oldest) >= 0 && string.CompareOrdinal(e.ExDate, newest) <= 0).ToList();
                if (applicable.Count > 0)
                    quotes = AdjustedPriceService.Adjust(quotes, applicable);
            }

            var high52 = quotes.Max(q => q.High ?? q.Close);
            var low52 = quotes.Min(q => q.Low ?? q.Close);
            var latestClose = quotes[^1].Close;   // 最新日還原後＝未還原（事件僅調整其之前的歷史價）
            result[code] = WatchlistSignalCalculator.NearFiftyTwoWeek(latestClose, high52, low52);
        }
        return result;
    }

    private static async Task<bool> TableExistsAsync(SqliteConnection connection, string table)
        => await connection.ExecuteScalarAsync<string?>(
            "SELECT name FROM sqlite_master WHERE type='table' AND name=@table", new { table }) is not null;
}
