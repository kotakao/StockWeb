using Dapper;
using StockWeb.Models;

namespace StockWeb.Data;

/// <summary>
/// 以唯讀連線查詢 market_daily。SQL 為固定字串、參數化 days，不涉及任何寫入或 DDL。
/// 欄位以 AS 別名對齊 MarketDailyRow（Dapper 預設不折疊底線）；回傳日期新到舊。
/// </summary>
public sealed class MarketRepository : IMarketRepository
{
    private readonly IDbConnectionFactory _connectionFactory;

    public MarketRepository(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    private const string DailySql = """
        SELECT date AS Date, index_close AS IndexClose, index_change_pct AS IndexChangePct,
               turnover AS Turnover, up_count AS UpCount, down_count AS DownCount,
               foreign_net AS ForeignNet, trust_net AS TrustNet, dealer_net AS DealerNet,
               margin_balance AS MarginBalance, short_balance AS ShortBalance
        FROM market_daily
        WHERE market = 'TWSE'
        ORDER BY date DESC
        LIMIT @days
        """;

    public async Task<IReadOnlyList<MarketDailyRow>> GetDailyAsync(int days)
    {
        using var connection = _connectionFactory.CreateReadOnly();
        var rows = await connection.QueryAsync<MarketDailyRow>(DailySql, new { days });
        return rows.ToList();
    }
}
