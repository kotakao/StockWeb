using Dapper;
using StockWeb.Models;

namespace StockWeb.Data;

/// <summary>
/// holdings 存取。讀取走唯讀連線；寫入（upsert/DELETE）走讀寫連線且只觸及 holdings 一表（§3 鐵律）。
/// upsert 的衝突鍵、欄位與 updated_at 格式與 Bot storage.upsert_holding 一致（同鍵重跑覆蓋、冪等）。
/// </summary>
public sealed class HoldingsRepository : IHoldingsRepository
{
    private readonly IDbConnectionFactory _connectionFactory;

    public HoldingsRepository(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    private const string ListSql = """
        SELECT code AS Code, shares AS Shares, avg_cost AS AvgCost, updated_at AS UpdatedAt
        FROM holdings
        WHERE user_id = @uid
        ORDER BY code
        """;

    public async Task<IReadOnlyList<Holding>> GetAsync()
    {
        using var connection = _connectionFactory.CreateReadOnly();
        var rows = await connection.QueryAsync<Holding>(ListSql, new { uid = WatchlistRepository.WebUserId });
        return rows.ToList();
    }

    private const string UpsertSql = """
        INSERT INTO holdings (user_id, code, shares, avg_cost, updated_at)
        VALUES (@uid, @code, @shares, @avgCost, @updatedAt)
        ON CONFLICT(user_id, code) DO UPDATE SET
            shares = excluded.shares, avg_cost = excluded.avg_cost, updated_at = excluded.updated_at
        """;

    public async Task UpsertAsync(string code, double shares, double avgCost)
    {
        using var connection = _connectionFactory.CreateReadWrite();
        await connection.ExecuteAsync(UpsertSql, new
        {
            uid = WatchlistRepository.WebUserId,
            code,
            shares,
            avgCost,
            updatedAt = DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss"),   // 對齊 Bot isoformat(timespec="seconds")
        });
    }

    public async Task<bool> RemoveAsync(string code)
    {
        using var connection = _connectionFactory.CreateReadWrite();
        var affected = await connection.ExecuteAsync(
            "DELETE FROM holdings WHERE user_id = @uid AND code = @code",
            new { uid = WatchlistRepository.WebUserId, code });
        return affected > 0;
    }
}
