using Dapper;
using StockWeb.Data;

namespace StockWeb.Tests;

/// <summary>
/// 持股寫入/讀取測試：upsert 新增與覆蓋、刪除、清單讀取，以及「寫入僅觸及 holdings」的白名單約束
/// （§3 鐵律：可寫表僅 watchlist 與 holdings；holdings 寫入不得更動其他表）。
/// </summary>
public class HoldingsRepositoryTests
{
    [Fact]
    public async Task Upsert_ThenGet_ReturnsHolding()
    {
        using var db = new TestDatabase();
        var repo = new HoldingsRepository(db.Factory);

        await repo.UpsertAsync("2330", 1000, 550.5);

        var row = Assert.Single(await repo.GetAsync());
        Assert.Equal("2330", row.Code);
        Assert.Equal(1000, row.Shares);
        Assert.Equal(550.5, row.AvgCost);
        Assert.False(string.IsNullOrWhiteSpace(row.UpdatedAt));   // updated_at 有寫入
    }

    [Fact]
    public async Task Upsert_SameCode_Overwrites()
    {
        using var db = new TestDatabase();
        var repo = new HoldingsRepository(db.Factory);

        await repo.UpsertAsync("2330", 1000, 500);
        await repo.UpsertAsync("2330", 2000, 480);   // 同鍵覆蓋

        var row = Assert.Single(await repo.GetAsync());
        Assert.Equal(2000, row.Shares);
        Assert.Equal(480, row.AvgCost);
    }

    [Fact]
    public async Task Remove_DeletesOnlyWhenPresent()
    {
        using var db = new TestDatabase();
        var repo = new HoldingsRepository(db.Factory);
        await repo.UpsertAsync("2330", 1000, 500);

        Assert.True(await repo.RemoveAsync("2330"));
        Assert.False(await repo.RemoveAsync("2330"));   // 已不存在
        Assert.Empty(await repo.GetAsync());
    }

    [Fact]
    public async Task Get_ReturnsOnlyWebUser_OrderedByCode()
    {
        using var db = new TestDatabase();
        // 另一使用者（Bot 端 user_id）之持股不應被網頁讀到。
        db.Execute("INSERT INTO holdings (user_id, code, shares, avg_cost) VALUES ('123', '2454', 100, 900);");
        var repo = new HoldingsRepository(db.Factory);
        await repo.UpsertAsync("2317", 3000, 100);
        await repo.UpsertAsync("2330", 1000, 500);

        var rows = await repo.GetAsync();

        Assert.Equal(new[] { "2317", "2330" }, rows.Select(r => r.Code).ToArray());
    }

    [Fact]
    public async Task Write_TouchesOnlyHoldings_LeavesOtherTablesUnchanged()
    {
        using var db = new TestDatabase();
        // 播種其他表：市場資料（唯讀）與 watchlist（另一可寫表）。
        db.Execute("INSERT INTO daily_quotes (market, date, code, name, close) VALUES ('TWSE','2025-01-08','2330','台積電',600);");
        db.Execute("INSERT INTO watchlist (user_id, code) VALUES ('0', '2330');");

        var repo = new HoldingsRepository(db.Factory);
        await repo.UpsertAsync("2330", 1000, 500);
        await repo.UpsertAsync("2330", 1500, 490);
        await repo.RemoveAsync("2330");

        using var conn = db.Factory.CreateReadOnly();
        // 白名單約束：holdings 以外的表筆數與內容不受影響。
        Assert.Equal(1, conn.ExecuteScalar<long>("SELECT COUNT(*) FROM daily_quotes;"));
        Assert.Equal(600d, conn.ExecuteScalar<double>("SELECT close FROM daily_quotes WHERE code='2330';"));
        Assert.Equal(1, conn.ExecuteScalar<long>("SELECT COUNT(*) FROM watchlist;"));
    }
}
