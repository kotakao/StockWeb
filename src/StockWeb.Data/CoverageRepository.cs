using Dapper;
using StockWeb.Models;

namespace StockWeb.Data;

/// <summary>
/// 以唯讀連線查詢各表的 MIN/MAX 日期與 distinct 日數。
/// 日期欄位依 storage.py 定義：多數表為 date，dividend_events 為 ex_date，monthly_revenue 為 year_month。
/// 查詢為固定字串（無使用者輸入），不涉及任何寫入或 DDL。
/// </summary>
public sealed class CoverageRepository : ICoverageRepository
{
    private readonly IDbConnectionFactory _connectionFactory;

    public CoverageRepository(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    // 各表與其日期欄位（固定內部清單，非使用者輸入，故可安全拼入 SQL）。
    // monthly_revenue 自部署起才累積、dividend_events 亦可能尚未建立，故查詢須容忍表不存在。
    private static readonly (string Table, string DateColumn)[] Targets =
    [
        ("daily_quotes", "date"),
        ("institutional", "date"),
        ("margin", "date"),
        ("valuation", "date"),
        ("market_daily", "date"),
        ("dividend_events", "ex_date"),
        ("monthly_revenue", "year_month"),
    ];

    public async Task<IReadOnlyList<CoverageRow>> GetCoverageAsync()
    {
        using var connection = _connectionFactory.CreateReadOnly();

        var existing = (await connection.QueryAsync<string>(
                "SELECT name FROM sqlite_master WHERE type='table'"))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var present = Targets.Where(t => existing.Contains(t.Table)).ToArray();

        var rowsByTable = new Dictionary<string, CoverageRow>(StringComparer.OrdinalIgnoreCase);
        if (present.Length > 0)
        {
            var sql = string.Join("\nUNION ALL\n", present.Select(t =>
                $"SELECT '{t.Table}' AS TableName, MIN({t.DateColumn}) AS MinDate, " +
                $"MAX({t.DateColumn}) AS MaxDate, COUNT(DISTINCT {t.DateColumn}) AS DistinctDays FROM {t.Table}"));

            foreach (var row in await connection.QueryAsync<CoverageRow>(sql))
                rowsByTable[row.TableName] = row;
        }

        // 依固定順序輸出；尚未建立的表以 0 日數、空日期呈現。
        return Targets
            .Select(t => rowsByTable.TryGetValue(t.Table, out var row)
                ? row
                : new CoverageRow { TableName = t.Table })
            .ToList();
    }
}
