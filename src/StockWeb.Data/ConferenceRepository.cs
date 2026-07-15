using Dapper;
using StockWeb.Models;
using StockWeb.Services;

namespace StockWeb.Data;

/// <summary>
/// investor_conferences 唯讀查詢。fact_date／announce_date 已由 Bot 端正規化為 ISO；
/// description 為原始全文，於此呼叫 ConferenceParser（讀取端計算，比照 StockRepository 慣例）解析召開細節。
/// 該表由 DC-K 建立，正式 DB 尚未建立時容忍表不存在回空集合（比照 dividend_events）。
/// </summary>
public sealed class ConferenceRepository : IConferenceRepository
{
    private readonly IDbConnectionFactory _connectionFactory;

    public ConferenceRepository(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    // InWatchlist 由 SQLite 整數 0/1 帶回，用 long 承接（Dapper 位置建構子對映需型別相符），於 ToConference 轉 bool。
    private sealed record RawRow(
        string Code, string? Name, string? Subject,
        string? AnnounceDate, string? FactDate, string? Description, long InWatchlist);

    private const string ByCodeSql = """
        SELECT code AS Code, name AS Name, subject AS Subject,
               announce_date AS AnnounceDate, fact_date AS FactDate, description AS Description,
               0 AS InWatchlist
        FROM investor_conferences
        WHERE code = @code
        ORDER BY fact_date DESC, announce_date DESC
        LIMIT @limit
        """;

    public async Task<IReadOnlyList<Conference>> GetByCodeAsync(string code, int limit)
    {
        using var connection = _connectionFactory.CreateReadOnly();
        if (await connection.ExecuteScalarAsync<string?>(TableExistsSql) is null)
            return Array.Empty<Conference>();

        var rows = await connection.QueryAsync<RawRow>(ByCodeSql, new { code, limit });
        return rows.Select(ToConference).ToList();
    }

    private const string ByMonthSql = """
        SELECT c.code AS Code, c.name AS Name, c.subject AS Subject,
               c.announce_date AS AnnounceDate, c.fact_date AS FactDate, c.description AS Description,
               CASE WHEN w.code IS NOT NULL THEN 1 ELSE 0 END AS InWatchlist
        FROM investor_conferences c
        LEFT JOIN watchlist w ON w.code = c.code AND w.user_id = @uid
        WHERE c.fact_date >= @start AND c.fact_date < @end
        ORDER BY c.fact_date, c.code
        """;

    public async Task<IReadOnlyList<Conference>> GetByMonthAsync(DateOnly monthStart)
    {
        using var connection = _connectionFactory.CreateReadOnly();
        if (await connection.ExecuteScalarAsync<string?>(TableExistsSql) is null)
            return Array.Empty<Conference>();

        var start = monthStart.ToString("yyyy-MM-dd");
        var end = monthStart.AddMonths(1).ToString("yyyy-MM-dd");
        var rows = await connection.QueryAsync<RawRow>(
            ByMonthSql, new { start, end, uid = WatchlistRepository.WebUserId });
        return rows.Select(ToConference).ToList();
    }

    private const string TableExistsSql =
        "SELECT name FROM sqlite_master WHERE type='table' AND name='investor_conferences'";

    private static Conference ToConference(RawRow r) => new(
        r.Code, r.Name, r.Subject, r.AnnounceDate, r.FactDate, r.Description,
        ConferenceParser.Parse(r.Description), r.InWatchlist != 0);
}
