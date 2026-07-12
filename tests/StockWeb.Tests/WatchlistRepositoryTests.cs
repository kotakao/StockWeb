using System.Diagnostics;
using Dapper;
using StockWeb.Data;
using StockWeb.Models;

namespace StockWeb.Tests;

/// <summary>
/// 自選股寫入/讀取的單元測試：代號存在性、上限 20 檔、重複、移除、清單組合查詢，
/// 以及並行寫入時 busy_timeout 生效（等待寫鎖而非立即失敗）。
/// </summary>
public class WatchlistRepositoryTests
{
    private static void SeedQuote(TestDatabase db, string code, string market = "TWSE",
        string date = "2025-01-08", double close = 100, double change = 0)
    {
        db.Execute(
            "INSERT INTO daily_quotes (market, date, code, name, close, change) " +
            "VALUES (@market, @date, @code, @code, @close, @change);",
            new { market, date, code, close, change });
    }

    // ---- 加入：存在性 / 上限 / 重複 ----

    [Fact]
    public async Task Add_UnknownCode_ReturnsNotFound()
    {
        using var db = new TestDatabase();
        var repo = new WatchlistRepository(db.Factory);

        Assert.Equal(WatchlistAddResult.NotFound, await repo.AddAsync("9999"));
    }

    [Fact]
    public async Task Add_ExistingCode_IsAddedThenExists()
    {
        using var db = new TestDatabase();
        SeedQuote(db, "2330");
        var repo = new WatchlistRepository(db.Factory);

        Assert.Equal(WatchlistAddResult.Added, await repo.AddAsync("2330"));
        Assert.Equal(WatchlistAddResult.Exists, await repo.AddAsync("2330"));
    }

    [Fact]
    public async Task Add_RejectsWhenLimitReached()
    {
        using var db = new TestDatabase();
        for (var i = 0; i < WatchlistRepository.Limit + 1; i++)
            SeedQuote(db, $"C{i:D4}");
        var repo = new WatchlistRepository(db.Factory);

        for (var i = 0; i < WatchlistRepository.Limit; i++)
            Assert.Equal(WatchlistAddResult.Added, await repo.AddAsync($"C{i:D4}"));

        // 第 21 檔應被上限擋下，清單仍維持 20 檔。
        Assert.Equal(WatchlistAddResult.Full, await repo.AddAsync($"C{WatchlistRepository.Limit:D4}"));

        using var conn = db.Factory.CreateReadOnly();
        Assert.Equal(WatchlistRepository.Limit,
            conn.ExecuteScalar<long>("SELECT COUNT(*) FROM watchlist WHERE user_id = '0';"));
    }

    [Fact]
    public async Task Remove_DeletesOnlyWhenPresent()
    {
        using var db = new TestDatabase();
        SeedQuote(db, "2330");
        var repo = new WatchlistRepository(db.Factory);
        await repo.AddAsync("2330");

        Assert.True(await repo.RemoveAsync("2330"));
        Assert.False(await repo.RemoveAsync("2330")); // 已不存在
    }

    // ---- 清單組合查詢 ----

    [Fact]
    public async Task Get_CombinesLatestQuote_FiveDayInstitutional_MarginChange()
    {
        using var db = new TestDatabase();
        SeedQuote(db, "2330", close: 110, change: 10); // 前收 100 → +10%
        db.Execute("INSERT INTO watchlist (user_id, code) VALUES ('0', '2330');");

        // 近 6 日法人：只計最近 5 日（最舊一日 d0 應被排除）。
        db.Execute("""
            INSERT INTO institutional (market, date, code, foreign_net, trust_net, dealer_net) VALUES
                ('TWSE','2025-01-01','2330', 1000000, 0, 0),   -- d0：超出近 5 日窗，不計
                ('TWSE','2025-01-02','2330', 1000, 500, -200),
                ('TWSE','2025-01-03','2330', 2000, 0, 0),
                ('TWSE','2025-01-06','2330', 0, 300, 0),
                ('TWSE','2025-01-07','2330', -500, 0, 100),
                ('TWSE','2025-01-08','2330', 4000, 200, 0);
            """);
        // 融資餘額變化取最新日：18000 - 15000 = 3000。
        db.Execute("""
            INSERT INTO margin (market, date, code, margin_balance, margin_prev) VALUES
                ('TWSE','2025-01-07','2330', 15000, 14000),
                ('TWSE','2025-01-08','2330', 18000, 15000);
            """);

        var rows = await new WatchlistRepository(db.Factory).GetAsync();

        var row = Assert.Single(rows);
        Assert.Equal("2330", row.Code);
        Assert.Equal(110, row.Close);
        Assert.Equal(10, row.ChangePct);
        // 近 5 日合計 = (1000+500-200)+(2000)+(300)+(-500+100)+(4000+200) = 7400。
        Assert.Equal(7400, row.InstitutionalNet5);
        Assert.Equal(3000, row.MarginChange);
    }

    [Fact]
    public async Task Get_MissingSideData_YieldsNulls_NotError()
    {
        using var db = new TestDatabase();
        // 代號在自選但無任何行情/法人/融資資料。
        db.Execute("INSERT INTO watchlist (user_id, code) VALUES ('0', '4444');");

        var rows = await new WatchlistRepository(db.Factory).GetAsync();

        var row = Assert.Single(rows);
        Assert.Equal("4444", row.Code);
        Assert.Null(row.Close);
        Assert.Null(row.InstitutionalNet5);
        Assert.Null(row.MarginChange);
    }

    // ---- 並行寫入：busy_timeout 生效 ----

    [Fact]
    public async Task Add_WaitsForConcurrentWriteLock_InsteadOfFailingFast()
    {
        using var db = new TestDatabase();
        SeedQuote(db, "2330");
        var repo = new WatchlistRepository(db.Factory);

        // 另一連線先取得寫鎖（延後 commit），模擬 Bot 17:00 寫入窗口。
        using var blocker = db.Factory.CreateReadWrite();
        using var tx = blocker.BeginTransaction();
        blocker.Execute("INSERT INTO watchlist (user_id, code) VALUES ('0', '1101');", transaction: tx);

        var release = Task.Run(async () =>
        {
            await Task.Delay(500);
            tx.Commit();
        });

        // 此加入須等待寫鎖釋放（busy_timeout=5000）而非立即拋 SQLITE_BUSY。
        var sw = Stopwatch.StartNew();
        var result = await repo.AddAsync("2330");
        sw.Stop();
        await release;

        Assert.Equal(WatchlistAddResult.Added, result);
        Assert.True(sw.ElapsedMilliseconds >= 400,
            $"應等待寫鎖釋放才寫入，實際僅 {sw.ElapsedMilliseconds}ms（busy_timeout 未生效？）");

        using var conn = db.Factory.CreateReadOnly();
        Assert.Equal(2, conn.ExecuteScalar<long>("SELECT COUNT(*) FROM watchlist WHERE user_id = '0';"));
    }
}
