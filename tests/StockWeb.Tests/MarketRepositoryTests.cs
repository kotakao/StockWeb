using StockWeb.Data;

namespace StockWeb.Tests;

public class MarketRepositoryTests
{
    [Fact]
    public async Task GetDailyAsync_ReturnsNewestFirst_AndLimitsToDays()
    {
        using var db = new TestDatabase();
        db.Execute("""
            INSERT INTO market_daily (market, date, index_close, turnover) VALUES
                ('TWSE', '2025-01-02', 18000, 3e11),
                ('TWSE', '2025-01-03', 18100, 3.2e11),
                ('TWSE', '2025-01-06', 18050, 2.9e11);
            """);
        var repository = new MarketRepository(db.Factory);

        var rows = await repository.GetDailyAsync(days: 2);

        Assert.Equal(2, rows.Count);
        Assert.Equal("2025-01-06", rows[0].Date);   // 新到舊
        Assert.Equal("2025-01-03", rows[1].Date);
        Assert.Equal(18050, rows[0].IndexClose);
    }

    [Fact]
    public async Task GetDailyAsync_FiltersToTwseMarket()
    {
        using var db = new TestDatabase();
        db.Execute("""
            INSERT INTO market_daily (market, date, index_close) VALUES
                ('TWSE', '2025-01-02', 18000),
                ('TPEX', '2025-01-02', 220);
            """);
        var repository = new MarketRepository(db.Factory);

        var rows = await repository.GetDailyAsync(days: 60);

        Assert.Single(rows);
        Assert.Equal(18000, rows[0].IndexClose);
    }
}
