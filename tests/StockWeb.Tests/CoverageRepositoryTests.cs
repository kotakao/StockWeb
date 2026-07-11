using StockWeb.Data;

namespace StockWeb.Tests;

public class CoverageRepositoryTests
{
    [Fact]
    public async Task GetCoverageAsync_EmptyDatabase_ReturnsSevenTablesWithZeroDays()
    {
        using var db = new TestDatabase();
        var repository = new CoverageRepository(db.Factory);

        var rows = await repository.GetCoverageAsync();

        Assert.Equal(7, rows.Count);
        Assert.All(rows, row =>
        {
            Assert.Null(row.MinDate);
            Assert.Null(row.MaxDate);
            Assert.Equal(0, row.DistinctDays);
        });
    }

    [Fact]
    public async Task GetCoverageAsync_DailyQuotes_ComputesMinMaxAndDistinctDays()
    {
        using var db = new TestDatabase();
        // 兩個 distinct 日期，其中一天有兩檔（distinct 日數應為 2，不受筆數影響）。
        db.Execute("""
            INSERT INTO daily_quotes (market, date, code, close) VALUES
                ('TWSE', '2025-01-02', '2330', 1000),
                ('TWSE', '2025-01-02', '2317', 100),
                ('TWSE', '2025-01-03', '2330', 1010);
            """);
        var repository = new CoverageRepository(db.Factory);

        var rows = await repository.GetCoverageAsync();
        var dailyQuotes = rows.Single(r => r.TableName == "daily_quotes");

        Assert.Equal("2025-01-02", dailyQuotes.MinDate);
        Assert.Equal("2025-01-03", dailyQuotes.MaxDate);
        Assert.Equal(2, dailyQuotes.DistinctDays);
    }

    [Fact]
    public async Task GetCoverageAsync_DividendEvents_UsesExDateColumn()
    {
        using var db = new TestDatabase();
        db.Execute("""
            INSERT INTO dividend_events (market, code, ex_date, event_type, cash_dividend) VALUES
                ('TWSE', '2330', '2025-06-18', 'cash', 3.5),
                ('TWSE', '2317', '2025-07-10', 'cash', 1.2);
            """);
        var repository = new CoverageRepository(db.Factory);

        var rows = await repository.GetCoverageAsync();
        var dividends = rows.Single(r => r.TableName == "dividend_events");

        Assert.Equal("2025-06-18", dividends.MinDate);
        Assert.Equal("2025-07-10", dividends.MaxDate);
        Assert.Equal(2, dividends.DistinctDays);
    }

    [Fact]
    public async Task GetCoverageAsync_MissingTable_ReturnsZeroDaysInsteadOfThrowing()
    {
        using var db = new TestDatabase();
        // 模擬正式 DB 尚未建立 monthly_revenue 的情境（該表自部署起才累積）。
        db.Execute("DROP TABLE monthly_revenue;");
        var repository = new CoverageRepository(db.Factory);

        var rows = await repository.GetCoverageAsync();
        var revenue = rows.Single(r => r.TableName == "monthly_revenue");

        Assert.Equal(7, rows.Count);
        Assert.Null(revenue.MinDate);
        Assert.Equal(0, revenue.DistinctDays);
    }

    [Fact]
    public async Task GetCoverageAsync_MonthlyRevenue_UsesYearMonthColumn()
    {
        using var db = new TestDatabase();
        db.Execute("""
            INSERT INTO monthly_revenue (market, code, year_month, revenue) VALUES
                ('TWSE', '2330', '2025-05', 1000),
                ('TWSE', '2330', '2025-06', 1100);
            """);
        var repository = new CoverageRepository(db.Factory);

        var rows = await repository.GetCoverageAsync();
        var revenue = rows.Single(r => r.TableName == "monthly_revenue");

        Assert.Equal("2025-05", revenue.MinDate);
        Assert.Equal("2025-06", revenue.MaxDate);
        Assert.Equal(2, revenue.DistinctDays);
    }
}
