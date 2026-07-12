using StockWeb.Data;

namespace StockWeb.Tests;

/// <summary>
/// 除權息行事曆查詢的單元測試：月界半開區間（含月首、不含次月首）、自選股高亮旗標、
/// dividend_events 表不存在時容忍回空。
/// </summary>
public class CalendarRepositoryTests
{
    private static void SeedEvent(TestDatabase db, string code, string exDate)
    {
        db.Execute(
            "INSERT INTO dividend_events (market, code, ex_date, name, event_type, cash_dividend, stock_ratio) " +
            "VALUES ('TWSE', @code, @exDate, @code, '除息', 1.0, 0);",
            new { code, exDate });
    }

    [Fact]
    public async Task Dividends_MonthBoundary_IncludesFirstDay_ExcludesNextMonth()
    {
        using var db = new TestDatabase();
        SeedEvent(db, "A", "2026-07-31"); // 前月最後一天 → 不含
        SeedEvent(db, "B", "2026-08-01"); // 當月第一天 → 含
        SeedEvent(db, "C", "2026-08-15"); // 當月 → 含
        SeedEvent(db, "D", "2026-08-31"); // 當月最後一天 → 含
        SeedEvent(db, "E", "2026-09-01"); // 次月第一天 → 不含

        var rows = await new CalendarRepository(db.Factory).GetDividendsAsync(new DateOnly(2026, 8, 1));

        Assert.Equal(new[] { "B", "C", "D" }, rows.Select(r => r.Code).ToArray());
    }

    [Fact]
    public async Task Dividends_FlagsWatchlistEvents()
    {
        using var db = new TestDatabase();
        SeedEvent(db, "2330", "2026-08-10");
        SeedEvent(db, "2317", "2026-08-12");
        db.Execute("INSERT INTO watchlist (user_id, code) VALUES ('0', '2330');");

        var rows = await new CalendarRepository(db.Factory).GetDividendsAsync(new DateOnly(2026, 8, 1));

        Assert.True(rows.Single(r => r.Code == "2330").InWatchlist);
        Assert.False(rows.Single(r => r.Code == "2317").InWatchlist);
    }

    [Fact]
    public async Task Dividends_MissingTable_ReturnsEmpty()
    {
        using var db = new TestDatabase();
        db.Execute("DROP TABLE dividend_events;"); // 模擬正式 DB 尚未建立該表

        var rows = await new CalendarRepository(db.Factory).GetDividendsAsync(new DateOnly(2026, 8, 1));

        Assert.Empty(rows);
    }
}
