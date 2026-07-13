using Dapper;
using StockWeb.Models;

namespace StockWeb.Data;

/// <summary>
/// 以唯讀連線查詢單月除權息事件（ex_date 為 ISO 字串，以半開區間 [月首, 次月首) 過濾月界）。
/// dividend_events 自部署起才累積、可能尚未建立，故容忍表不存在時回空集合。
/// </summary>
public sealed class CalendarRepository : ICalendarRepository
{
    private readonly IDbConnectionFactory _connectionFactory;

    public CalendarRepository(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    private const string DividendsSql = """
        SELECT d.code AS Code, d.name AS Name, d.ex_date AS ExDate, d.event_type AS EventType,
               d.cash_dividend AS CashDividend, d.stock_ratio AS StockRatio,
               CASE WHEN w.code IS NOT NULL THEN 1 ELSE 0 END AS InWatchlist
        FROM dividend_events d
        LEFT JOIN watchlist w ON w.code = d.code AND w.user_id = @uid
        WHERE d.ex_date >= @start AND d.ex_date < @end
        ORDER BY d.ex_date, d.code
        """;

    public async Task<IReadOnlyList<DividendEvent>> GetDividendsAsync(DateOnly monthStart)
    {
        using var connection = _connectionFactory.CreateReadOnly();

        var tableExists = await connection.ExecuteScalarAsync<string?>(
            "SELECT name FROM sqlite_master WHERE type='table' AND name='dividend_events'");
        if (tableExists is null)
            return Array.Empty<DividendEvent>();

        var start = monthStart.ToString("yyyy-MM-dd");
        var end = monthStart.AddMonths(1).ToString("yyyy-MM-dd");
        var rows = await connection.QueryAsync<DividendEvent>(
            DividendsSql, new { start, end, uid = WatchlistRepository.WebUserId });
        return rows.ToList();
    }
}
