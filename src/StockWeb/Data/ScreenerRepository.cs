using Dapper;
using Microsoft.Data.Sqlite;
using StockWeb.Models;
using StockWeb.Services;

namespace StockWeb.Data;

/// <summary>
/// 以唯讀連線執行 ScreenerQueryBuilder 組出的參數化 SQL。SQL 骨架固定、使用者輸入全走 @參數，
/// 不涉及任何寫入或 DDL（§3 鐵律）。
/// </summary>
public sealed class ScreenerRepository : IScreenerRepository
{
    private readonly IDbConnectionFactory _connectionFactory;

    public ScreenerRepository(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<IReadOnlyList<ScreenerRow>> ScreenAsync(ScreenerCriteria criteria)
    {
        using var connection = _connectionFactory.CreateReadOnly();

        // monthly_revenue 自部署起才累積、可能尚未建立（比照 CoverageRepository 的容忍）。
        // revenue_yoy 條件所依賴的表不存在時，沒有任何股票能被證明符合該條件 → 回空集合，避免查詢丟「no such table」。
        if (criteria.RevenueYoyMin is not null &&
            !await TableExistsAsync(connection, "monthly_revenue"))
        {
            return Array.Empty<ScreenerRow>();
        }

        var query = ScreenerQueryBuilder.Build(criteria);

        var dynamic = new DynamicParameters();
        foreach (var (name, value) in query.Parameters)
            dynamic.Add(name, value);

        var rows = await connection.QueryAsync<ScreenerRow>(query.Sql, dynamic);
        return rows.ToList();
    }

    private static async Task<bool> TableExistsAsync(SqliteConnection connection, string table)
    {
        var name = await connection.ExecuteScalarAsync<string?>(
            "SELECT name FROM sqlite_master WHERE type='table' AND name=@table", new { table });
        return name is not null;
    }
}
