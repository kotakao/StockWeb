using StockWeb.Data;

namespace StockWeb.Tests;

/// <summary>
/// 個股六端點的資料層測試：查無代號友善回空、序列由舊到新、days/months 上限截取、
/// 前復權整合（數字與公式一致）、上市/上櫃市場欄、dividend_events / monthly_revenue 表不存在時容忍回空。
/// </summary>
public class StockRepositoryTests
{
    private static void SeedQuote(TestDatabase db, string code, string date, double close,
        double volume = 1000, string market = "TWSE", string name = "測試")
    {
        db.Execute(
            "INSERT INTO daily_quotes (market, date, code, name, open, high, low, close, change, volume) " +
            "VALUES (@market, @date, @code, @name, @close, @close, @close, @close, 0, @volume);",
            new { market, date, code, name, close, volume });
    }

    private static void SeedEvent(TestDatabase db, string code, string exDate, double cash, double ratio)
    {
        db.Execute(
            "INSERT INTO dividend_events (market, code, ex_date, name, event_type, cash_dividend, stock_ratio) " +
            "VALUES ('TWSE', @code, @exDate, '測試', '除權息', @cash, @ratio);",
            new { code, exDate, cash, ratio });
    }

    [Fact]
    public async Task Quotes_UnknownCode_ReturnsEmptyWithNullMeta()
    {
        using var db = new TestDatabase();
        SeedQuote(db, "2330", "2026-06-01", 100);

        var result = await new StockRepository(db.Factory).GetQuotesAsync("9999", 252, adjusted: true);

        Assert.Empty(result.Quotes);
        Assert.Null(result.Name);
        Assert.Null(result.Market);
    }

    [Fact]
    public async Task Quotes_ReturnsAscending_WithMarketAndName()
    {
        using var db = new TestDatabase();
        SeedQuote(db, "6488", "2026-06-03", 30, market: "TPEX", name: "環球晶");
        SeedQuote(db, "6488", "2026-06-01", 28, market: "TPEX", name: "環球晶");

        var result = await new StockRepository(db.Factory).GetQuotesAsync("6488", 252, adjusted: false);

        Assert.Equal(new[] { "2026-06-01", "2026-06-03" }, result.Quotes.Select(q => q.Date));  // 由舊到新
        Assert.Equal("TPEX", result.Market);
        Assert.Equal("環球晶", result.Name);
    }

    [Fact]
    public async Task Quotes_RespectsDaysLimit_KeepsNewest()
    {
        using var db = new TestDatabase();
        SeedQuote(db, "2330", "2026-06-01", 1);
        SeedQuote(db, "2330", "2026-06-02", 2);
        SeedQuote(db, "2330", "2026-06-03", 3);

        var result = await new StockRepository(db.Factory).GetQuotesAsync("2330", 2, adjusted: false);

        // 取最新 2 筆後由舊到新排列。
        Assert.Equal(new[] { "2026-06-02", "2026-06-03" }, result.Quotes.Select(q => q.Date));
    }

    [Fact]
    public async Task Quotes_Adjusted_AppliesCashDividendBeforeExDate()
    {
        using var db = new TestDatabase();
        SeedQuote(db, "2330", "2026-06-03", 100);
        SeedQuote(db, "2330", "2026-06-02", 100);
        SeedQuote(db, "2330", "2026-06-01", 100);
        SeedEvent(db, "2330", "2026-06-02", cash: 2, ratio: 0);

        var repo = new StockRepository(db.Factory);
        var adjusted = await repo.GetQuotesAsync("2330", 252, adjusted: true);
        var raw = await repo.GetQuotesAsync("2330", 252, adjusted: false);

        // 由舊到新：06-01 為除息前 → 減 2；06-02/06-03 不動；量不調整。
        Assert.Equal(98.0, adjusted.Quotes[0].Close!.Value, 6);
        Assert.Equal(100.0, adjusted.Quotes[1].Close!.Value, 6);
        Assert.Equal(100.0, adjusted.Quotes[2].Close!.Value, 6);
        Assert.Equal(100.0, raw.Quotes[0].Close!.Value, 6);   // 未還原不動
        Assert.Equal(1000, adjusted.Quotes[0].Volume!.Value);
    }

    [Fact]
    public async Task Quotes_Adjusted_IgnoresFutureEventOutsideRange()
    {
        using var db = new TestDatabase();
        SeedQuote(db, "2330", "2026-06-02", 100);
        SeedQuote(db, "2330", "2026-06-01", 100);
        SeedEvent(db, "2330", "2026-06-10", cash: 5, ratio: 0);   // 晚於序列最新日 → 不套用

        var adjusted = await new StockRepository(db.Factory).GetQuotesAsync("2330", 252, adjusted: true);

        Assert.All(adjusted.Quotes, q => Assert.Equal(100.0, q.Close!.Value, 6));
    }

    [Fact]
    public async Task Quotes_Adjusted_MissingDividendTable_ReturnsRawPrices()
    {
        using var db = new TestDatabase();
        SeedQuote(db, "2330", "2026-06-01", 100);
        db.Execute("DROP TABLE dividend_events;");

        var adjusted = await new StockRepository(db.Factory).GetQuotesAsync("2330", 252, adjusted: true);

        Assert.Equal(100.0, adjusted.Quotes[0].Close!.Value, 6);
    }

    [Fact]
    public async Task Institutional_ReturnsAscending()
    {
        using var db = new TestDatabase();
        db.Execute("INSERT INTO institutional (market, date, code, name, foreign_net, trust_net, dealer_net) " +
                   "VALUES ('TWSE','2026-06-02','2330','x',100,0,0),('TWSE','2026-06-01','2330','x',200,0,0);");

        var rows = await new StockRepository(db.Factory).GetInstitutionalAsync("2330", 60);

        Assert.Equal(new[] { "2026-06-01", "2026-06-02" }, rows.Select(r => r.Date));
    }

    [Fact]
    public async Task Margin_ReturnsAscending()
    {
        using var db = new TestDatabase();
        db.Execute("INSERT INTO margin (market, date, code, name, margin_balance, margin_prev, short_balance, short_prev) " +
                   "VALUES ('TWSE','2026-06-02','2330','x',500,480,20,18),('TWSE','2026-06-01','2330','x',480,470,18,15);");

        var rows = await new StockRepository(db.Factory).GetMarginAsync("2330", 60);

        Assert.Equal(new[] { "2026-06-01", "2026-06-02" }, rows.Select(r => r.Date));
        Assert.Equal(500, rows[^1].MarginBalance);
    }

    [Fact]
    public async Task Valuation_ReturnsAscending()
    {
        using var db = new TestDatabase();
        db.Execute("INSERT INTO valuation (market, date, code, pe, dividend_yield, pb) " +
                   "VALUES ('TWSE','2026-06-02','2330',20,2.5,5),('TWSE','2026-06-01','2330',18,2.8,4.8);");

        var rows = await new StockRepository(db.Factory).GetValuationAsync("2330", 252);

        Assert.Equal(new[] { "2026-06-01", "2026-06-02" }, rows.Select(r => r.Date));
    }

    [Fact]
    public async Task Revenue_ReturnsAscending_AndLimited()
    {
        using var db = new TestDatabase();
        db.Execute("INSERT INTO monthly_revenue (market, code, year_month, name, revenue, mom_pct, yoy_pct) " +
                   "VALUES ('TWSE','2330','2026-04','x',100,0,10),('TWSE','2330','2026-05','x',110,0,12),('TWSE','2330','2026-06','x',120,0,15);");

        var rows = await new StockRepository(db.Factory).GetRevenueAsync("2330", 2);

        Assert.Equal(new[] { "2026-05", "2026-06" }, rows.Select(r => r.YearMonth));   // 最新 2 月、由舊到新
    }

    [Fact]
    public async Task Revenue_MissingTable_ReturnsEmpty()
    {
        using var db = new TestDatabase();
        db.Execute("DROP TABLE monthly_revenue;");

        var rows = await new StockRepository(db.Factory).GetRevenueAsync("2330", 24);

        Assert.Empty(rows);
    }

    [Fact]
    public async Task Dividends_ReturnsChronological_AndToleratesMissingTable()
    {
        using var db = new TestDatabase();
        SeedEvent(db, "2330", "2026-06-10", 2, 0);
        SeedEvent(db, "2330", "2025-06-10", 1.5, 0.1);

        var repo = new StockRepository(db.Factory);
        var rows = await repo.GetDividendsAsync("2330");
        Assert.Equal(new[] { "2025-06-10", "2026-06-10" }, rows.Select(r => r.ExDate));

        db.Execute("DROP TABLE dividend_events;");
        Assert.Empty(await repo.GetDividendsAsync("2330"));
    }
}
