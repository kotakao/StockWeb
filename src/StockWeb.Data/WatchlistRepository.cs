using Dapper;
using StockWeb.Models;

namespace StockWeb.Data;

/// <summary>
/// watchlist 存取。讀取（清單組合、代號存在性）走唯讀連線；寫入（INSERT/DELETE）走讀寫連線，
/// 且讀寫連線只觸及 watchlist 一表（§3 鐵律）。上限與 user_id 與 Bot 一致。
/// </summary>
public sealed class WatchlistRepository : IWatchlistRepository
{
    /// <summary>網頁使用者的固定保留 user_id（§3）。</summary>
    public const string WebUserId = "0";

    /// <summary>每人自選股上限，與 Bot storage.WATCHLIST_LIMIT 一致。</summary>
    public const int Limit = 20;

    private readonly IDbConnectionFactory _connectionFactory;

    public WatchlistRepository(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    // 一次查詢組合：以自選代號 JOIN 最新日 daily_quotes，近 5 日法人淨額與融資變化以相關子查詢取得。
    // 代號在台股跨市場不重複，故僅以 code 關聯（不帶 market）。
    private const string ListSql = """
        SELECT
            w.code AS Code,
            q.name AS Name,
            q.close AS Close,
            CASE WHEN (q.close - q.change) <> 0
                 THEN ROUND(q.change / (q.close - q.change) * 100, 2) END AS ChangePct,
            (SELECT SUM(COALESCE(i.foreign_net, 0) + COALESCE(i.trust_net, 0) + COALESCE(i.dealer_net, 0))
               FROM (SELECT foreign_net, trust_net, dealer_net FROM institutional
                     WHERE code = w.code ORDER BY date DESC LIMIT 5) i) AS InstitutionalNet5,
            (SELECT m.margin_balance - m.margin_prev FROM margin m
               WHERE m.code = w.code ORDER BY m.date DESC LIMIT 1) AS MarginChange
        FROM watchlist w
        LEFT JOIN daily_quotes q
            ON q.code = w.code AND q.date = (SELECT MAX(date) FROM daily_quotes WHERE code = w.code)
        WHERE w.user_id = @uid
        ORDER BY w.code
        """;

    public async Task<IReadOnlyList<WatchlistRow>> GetAsync()
    {
        using var connection = _connectionFactory.CreateReadOnly();
        var rows = await connection.QueryAsync<WatchlistRow>(ListSql, new { uid = WebUserId });
        return rows.ToList();
    }

    public async Task<WatchlistAddResult> AddAsync(string code)
    {
        // 代號存在性檢查走唯讀連線（不讓讀寫連線觸及 daily_quotes）。
        using (var readOnly = _connectionFactory.CreateReadOnly())
        {
            var exists = await readOnly.ExecuteScalarAsync<long>(
                "SELECT COUNT(*) FROM daily_quotes WHERE code = @code", new { code });
            if (exists == 0)
                return WatchlistAddResult.NotFound;
        }

        // 讀寫連線只操作 watchlist：讀現況判成員/上限，再 INSERT。
        using var connection = _connectionFactory.CreateReadWrite();
        var current = (await connection.QueryAsync<string>(
            "SELECT code FROM watchlist WHERE user_id = @uid", new { uid = WebUserId })).ToList();

        if (current.Contains(code))
            return WatchlistAddResult.Exists;
        if (current.Count >= Limit)
            return WatchlistAddResult.Full;

        await connection.ExecuteAsync(
            "INSERT INTO watchlist (user_id, code) VALUES (@uid, @code)",
            new { uid = WebUserId, code });
        return WatchlistAddResult.Added;
    }

    public async Task<bool> RemoveAsync(string code)
    {
        using var connection = _connectionFactory.CreateReadWrite();
        var affected = await connection.ExecuteAsync(
            "DELETE FROM watchlist WHERE user_id = @uid AND code = @code",
            new { uid = WebUserId, code });
        return affected > 0;
    }
}
