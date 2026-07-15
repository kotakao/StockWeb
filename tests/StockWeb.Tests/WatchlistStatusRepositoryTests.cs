using Dapper;
using StockWeb.Data;
using StockWeb.Models;

namespace StockWeb.Tests;

/// <summary>
/// 觀察名單狀態板組合查詢測試：行情/持股損益/狀態訊號一次組合、聚合列、預設排序、
/// 未來 14 日事件旗標、還原後 52 週高低，以及事件表未建立時的容錯。
/// 視窗（未來 14 日、近一年）由 repository 以 DateTime.Today 推導，故測資日期一律相對今日計算。
/// </summary>
public class WatchlistStatusRepositoryTests
{
    private static string D(int daysFromToday) => DateTime.Today.AddDays(daysFromToday).ToString("yyyy-MM-dd");

    private static void SeedQuote(TestDatabase db, string code, string date,
        double? close = null, double change = 0, double? high = null, double? low = null)
    {
        db.Execute(
            "INSERT INTO daily_quotes (market, date, code, name, high, low, close, change) " +
            "VALUES ('TWSE', @date, @code, @code, @high, @low, @close, @change);",
            new { date, code, high = high ?? close, low = low ?? close, close, change });
    }

    [Fact]
    public async Task GetStatus_ComposesQuoteHoldingSignals_AndSummary()
    {
        using var db = new TestDatabase();
        db.Execute("INSERT INTO watchlist (user_id, code) VALUES ('0', '2330');");

        // 最新日：收盤 110、當日 +10（前收 100 → +10%）。一年內另有一日設定 52 週高低（高 115 / 低 50）。
        SeedQuote(db, "2330", D(-100), close: 60, high: 115, low: 50);
        SeedQuote(db, "2330", D(0), close: 110, change: 10, high: 110, low: 105);

        // 持股 1000 股、均價 100：市值 110000、未實現 +10000、報酬 +10%、當日 +10000。
        db.Execute("INSERT INTO holdings (user_id, code, shares, avg_cost) VALUES ('0','2330',1000,100);");

        // 近 5 日法人淨買超（合計為正 → 買超）。
        for (var i = 0; i < 5; i++)
            db.Execute("INSERT INTO institutional (market, date, code, foreign_net, trust_net, dealer_net) " +
                       "VALUES ('TWSE', @date, '2330', 1000, 0, 0);", new { date = D(-i) });

        // 融資餘額 6 日遞增：最新 − 第 6 新 = 150−100 > 0 → 增。
        for (var i = 0; i < 6; i++)
            db.Execute("INSERT INTO margin (market, date, code, margin_balance) VALUES ('TWSE', @date, '2330', @bal);",
                new { date = D(-i), bal = 150 - i * 10 });

        // 未來 14 日內有除權息（+7 日）與法說會（+10 日）。除權息 ex_date 在最新日之後，不參與還原。
        db.Execute("INSERT INTO dividend_events (market, code, ex_date, name, event_type, cash_dividend) " +
                   "VALUES ('TWSE','2330',@ex,'2330','息',5);", new { ex = D(7) });
        db.Execute("INSERT INTO investor_conferences (market, code, announce_date, announce_time, fact_date) " +
                   "VALUES ('TWSE','2330','2026-01-01','09:00',@fact);", new { fact = D(10) });

        var response = await new WatchlistStatusRepository(db.Factory).GetStatusAsync();

        var row = Assert.Single(response.Rows);
        Assert.Equal("2330", row.Code);
        Assert.Equal(110, row.Close);
        Assert.Equal(10, row.ChangePct);
        Assert.Equal(1000, row.Shares);
        Assert.Equal(110_000, row.MarketValue);
        Assert.Equal(10_000, row.UnrealizedPnl);
        Assert.Equal(10, row.ReturnRate);
        Assert.Equal(10_000, row.DayPnl);
        Assert.Equal(SignalTrend.Up, row.InstitutionalTrend);
        Assert.Equal(SignalTrend.Up, row.MarginTrend);
        Assert.Equal(RangeSignal.NearHigh, row.RangePosition);   // 收盤 110 ≥ 52 週高 115 × 0.95
        Assert.True(row.HasDividendSoon);
        Assert.True(row.HasConferenceSoon);

        Assert.Equal(110_000, response.Summary.TotalMarketValue);
        Assert.Equal(10_000, response.Summary.TotalUnrealizedPnl);
        Assert.Equal(10_000, response.Summary.TotalDayPnl);
    }

    [Fact]
    public async Task GetStatus_NoHolding_LeavesPnlNull_ButKeepsSignals()
    {
        using var db = new TestDatabase();
        db.Execute("INSERT INTO watchlist (user_id, code) VALUES ('0', '2317');");
        SeedQuote(db, "2317", D(0), close: 50, change: -1, high: 50, low: 48);

        var response = await new WatchlistStatusRepository(db.Factory).GetStatusAsync();

        var row = Assert.Single(response.Rows);
        Assert.Null(row.Shares);
        Assert.Null(row.MarketValue);
        Assert.Null(row.UnrealizedPnl);
        Assert.Null(row.ReturnRate);
        Assert.Null(row.DayPnl);
        Assert.Equal(50, row.Close);          // 純觀察仍顯示行情
        Assert.False(row.HasDividendSoon);
        Assert.Equal(0, response.Summary.TotalMarketValue);
    }

    [Fact]
    public async Task GetStatus_DefaultSort_ByDayPnlDescending_NoHoldingLast()
    {
        using var db = new TestDatabase();
        db.Execute("INSERT INTO watchlist (user_id, code) VALUES ('0','AAAA'),('0','BBBB'),('0','CCCC');");
        SeedQuote(db, "AAAA", D(0), close: 100, change: 1);   // 當日損益 = 1000×1 = 1000
        SeedQuote(db, "BBBB", D(0), close: 100, change: 5);   // 當日損益 = 1000×5 = 5000
        SeedQuote(db, "CCCC", D(0), close: 100, change: 9);   // 無持股 → 當日損益 null，殿後
        db.Execute("INSERT INTO holdings (user_id, code, shares, avg_cost) VALUES ('0','AAAA',1000,90),('0','BBBB',1000,90);");

        var response = await new WatchlistStatusRepository(db.Factory).GetStatusAsync();

        Assert.Equal(new[] { "BBBB", "AAAA", "CCCC" }, response.Rows.Select(r => r.Code).ToArray());
    }

    [Fact]
    public async Task GetStatus_AdjustedPrice_ShiftsFiftyTwoWeekHigh()
    {
        using var db = new TestDatabase();
        db.Execute("INSERT INTO watchlist (user_id, code) VALUES ('0','2330');");

        // 未還原：舊高 200、最新收盤 110 → 距高甚遠。除息 90 元（ex 在兩者之間）令舊高還原為 110，
        // 使最新收盤成為「近高」——證明 52 週高低走還原價。
        SeedQuote(db, "2330", D(-100), close: 200, high: 200, low: 200);
        SeedQuote(db, "2330", D(0), close: 110, change: 0, high: 110, low: 110);
        db.Execute("INSERT INTO dividend_events (market, code, ex_date, name, event_type, cash_dividend) " +
                   "VALUES ('TWSE','2330',@ex,'2330','息',90);", new { ex = D(-50) });

        var response = await new WatchlistStatusRepository(db.Factory).GetStatusAsync();

        Assert.Equal(RangeSignal.NearHigh, Assert.Single(response.Rows).RangePosition);
    }

    [Fact]
    public async Task GetStatus_ToleratesMissingConferenceTable()
    {
        using var db = new TestDatabase();
        db.Execute("DROP TABLE investor_conferences;");   // 測試用 DDL：模擬 DC-K 尚未於正式庫建立
        db.Execute("INSERT INTO watchlist (user_id, code) VALUES ('0','2330');");
        SeedQuote(db, "2330", D(0), close: 100, change: 0);

        var response = await new WatchlistStatusRepository(db.Factory).GetStatusAsync();

        var row = Assert.Single(response.Rows);
        Assert.False(row.HasConferenceSoon);   // 表不存在時視為無事件、不丟例外
    }

    [Fact]
    public async Task GetStatus_EmptyWatchlist_ReturnsEmptyWithZeroSummary()
    {
        using var db = new TestDatabase();

        var response = await new WatchlistStatusRepository(db.Factory).GetStatusAsync();

        Assert.Empty(response.Rows);
        Assert.Equal(0, response.Summary.TotalMarketValue);
    }
}
