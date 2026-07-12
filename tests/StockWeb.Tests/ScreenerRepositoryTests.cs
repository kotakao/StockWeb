using StockWeb.Data;
using StockWeb.Models;

namespace StockWeb.Tests;

/// <summary>
/// 各條件對真實 SQLite 執行的單元測試。fixture 造出邊界案例：連買恰好 N 日 / 中斷 / NULL 欄位、
/// 量能均量缺值、估值最新日取值、AND 組合、market 過濾與 200 列上限。
/// </summary>
public class ScreenerRepositoryTests
{
    private const string Latest = "2025-01-08";
    private static readonly string[] Dates = { "2025-01-02", "2025-01-03", "2025-01-06", "2025-01-07", "2025-01-08" };

    // 在最新日 (Latest) 放一列 daily_quotes，使該代號成為候選；close/change/volume 可覆寫。
    private static void SeedCandidate(TestDatabase db, string code, string market = "TWSE",
        double close = 100, double change = 0, double volume = 1000)
    {
        db.Execute(
            "INSERT INTO daily_quotes (market, date, code, name, close, change, volume) " +
            "VALUES (@market, @Latest, @code, @code, @close, @change, @volume);",
            new { market, Latest, code, close, change, volume });
    }

    private static async Task<IReadOnlyList<ScreenerRow>> Screen(TestDatabase db, ScreenerCriteria criteria)
        => await new ScreenerRepository(db.Factory).ScreenAsync(criteria);

    // ---- 三大法人連續淨買超 N 日 ----

    [Fact]
    public async Task ForeignBuyDays_HandlesExactN_Interrupted_Null_NoBreak()
    {
        using var db = new TestDatabase();
        foreach (var code in new[] { "A", "B", "C", "D", "E" })
            SeedCandidate(db, code);

        // foreign_net：d1..d5 對應 Dates。>0 為買超，<=0 或 NULL 為中斷。
        db.Execute("""
            INSERT INTO institutional (market, date, code, foreign_net) VALUES
                -- A：恰好連 3 日（d3,d4,d5>0；d2 中斷）
                ('TWSE','2025-01-02','A', 5), ('TWSE','2025-01-03','A', -5),
                ('TWSE','2025-01-06','A', 5), ('TWSE','2025-01-07','A', 5), ('TWSE','2025-01-08','A', 5),
                -- B：連 2 日後中斷（d3 中斷）→ streak 2
                ('TWSE','2025-01-02','B', 5), ('TWSE','2025-01-03','B', 5),
                ('TWSE','2025-01-06','B', -5), ('TWSE','2025-01-07','B', 5), ('TWSE','2025-01-08','B', 5),
                -- C：最新日 foreign_net 為 NULL → streak 0（NULL 視為中斷）
                ('TWSE','2025-01-06','C', 5), ('TWSE','2025-01-07','C', 5), ('TWSE','2025-01-08','C', NULL),
                -- D：連 4 日（d1 中斷）→ streak 4
                ('TWSE','2025-01-02','D', -5), ('TWSE','2025-01-03','D', 5),
                ('TWSE','2025-01-06','D', 5), ('TWSE','2025-01-07','D', 5), ('TWSE','2025-01-08','D', 5),
                -- E：全數買超、無中斷 → streak 5（走 COALESCE 空字串分支）
                ('TWSE','2025-01-02','E', 5), ('TWSE','2025-01-03','E', 5),
                ('TWSE','2025-01-06','E', 5), ('TWSE','2025-01-07','E', 5), ('TWSE','2025-01-08','E', 5);
            """);

        var rows = await Screen(db, new ScreenerCriteria { ForeignBuyDays = 3 });

        // 門檻 3：A(=3)、D(=4)、E(=5) 通過；B(=2)、C(=0) 被濾除。
        Assert.Equal(new[] { "A", "D", "E" }, rows.Select(r => r.Code).ToArray());
        Assert.Equal(3, rows.Single(r => r.Code == "A").ForeignBuyDays);
        Assert.Equal(4, rows.Single(r => r.Code == "D").ForeignBuyDays);
        Assert.Equal(5, rows.Single(r => r.Code == "E").ForeignBuyDays);
    }

    [Fact]
    public async Task TrustBuyDays_UsesTrustNet_IndependentOfForeign()
    {
        using var db = new TestDatabase();
        SeedCandidate(db, "P");
        SeedCandidate(db, "Q");
        db.Execute("""
            INSERT INTO institutional (market, date, code, foreign_net, trust_net) VALUES
                -- P：投信連 2 日買超；外資中斷（證明用 trust_net 而非 foreign_net）
                ('TWSE','2025-01-07','P', -9, 5), ('TWSE','2025-01-08','P', -9, 5),
                -- Q：投信最新日賣超 → streak 0
                ('TWSE','2025-01-07','Q', 9, 5), ('TWSE','2025-01-08','Q', 9, -1);
            """);

        var rows = await Screen(db, new ScreenerCriteria { TrustBuyDays = 2 });

        Assert.Equal(new[] { "P" }, rows.Select(r => r.Code).ToArray());
        Assert.Equal(2, rows[0].TrustBuyDays);
    }

    // ---- 量能倍數（當日量 / 近 5 日均量，不含當日）----

    [Fact]
    public async Task VolumeMultiple_ComparesAgainstPriorFiveDayAverage_BoundaryAndNull()
    {
        using var db = new TestDatabase();
        // 各代號最新日量能不同；先放最新日候選列（覆寫 volume）。
        SeedCandidate(db, "V1", volume: 300); // 均量 100 → 3.0 倍，過
        SeedCandidate(db, "V2", volume: 100); // 均量 100 → 1.0 倍，不過
        SeedCandidate(db, "V3", volume: 500); // 無前值 → 均量 NULL → 不過
        SeedCandidate(db, "V4", volume: 200); // 均量 100 → 恰 2.0 倍，過（>=）

        // 前值（date < Latest）：V1/V2/V4 各四日均量 100；V3 完全無前值。
        db.Execute("""
            INSERT INTO daily_quotes (market, date, code, name, close, change, volume) VALUES
                ('TWSE','2025-01-02','V1','V1',100,0,100), ('TWSE','2025-01-03','V1','V1',100,0,100),
                ('TWSE','2025-01-06','V1','V1',100,0,100), ('TWSE','2025-01-07','V1','V1',100,0,100),
                ('TWSE','2025-01-02','V2','V2',100,0,100), ('TWSE','2025-01-03','V2','V2',100,0,100),
                ('TWSE','2025-01-06','V2','V2',100,0,100), ('TWSE','2025-01-07','V2','V2',100,0,100),
                ('TWSE','2025-01-02','V4','V4',100,0,100), ('TWSE','2025-01-03','V4','V4',100,0,100),
                ('TWSE','2025-01-06','V4','V4',100,0,100), ('TWSE','2025-01-07','V4','V4',100,0,100);
            """);

        var rows = await Screen(db, new ScreenerCriteria { VolumeMultipleMin = 2 });

        Assert.Equal(new[] { "V1", "V4" }, rows.Select(r => r.Code).ToArray());
        Assert.Equal(3.0, rows.Single(r => r.Code == "V1").VolumeMultiple);
        Assert.Equal(2.0, rows.Single(r => r.Code == "V4").VolumeMultiple);
    }

    // ---- 估值（valuation 最新日）----

    [Fact]
    public async Task PeMax_UsesLatestValuationDate_ExcludesNull_BoundaryInclusive()
    {
        using var db = new TestDatabase();
        foreach (var code in new[] { "L", "H", "N", "B" })
            SeedCandidate(db, code);

        // 舊日 valuation 的 pe 皆很低（若誤用舊日會全數通過），最新日才是判斷依據。
        db.Execute("""
            INSERT INTO valuation (market, date, code, pe) VALUES
                ('TWSE','2025-01-07','L', 1), ('TWSE','2025-01-07','H', 1),
                ('TWSE','2025-01-07','N', 1), ('TWSE','2025-01-07','B', 1),
                ('TWSE','2025-01-08','L', 10),   -- 低於上限 → 過
                ('TWSE','2025-01-08','H', 20),   -- 高於上限 → 不過
                ('TWSE','2025-01-08','N', NULL), -- NULL → 不過
                ('TWSE','2025-01-08','B', 15);   -- 恰上限 → 過（<=）
            """);

        var rows = await Screen(db, new ScreenerCriteria { PeMax = 15 });

        Assert.Equal(new[] { "B", "L" }, rows.Select(r => r.Code).OrderBy(c => c).ToArray());
        Assert.Equal(10, rows.Single(r => r.Code == "L").Pe);
    }

    [Fact]
    public async Task DividendYieldMin_And_PbMax_FilterAndReportValues()
    {
        using var db = new TestDatabase();
        SeedCandidate(db, "G");  // good：殖利率高、pb 低
        SeedCandidate(db, "X");  // 殖利率不足
        db.Execute("""
            INSERT INTO valuation (market, date, code, dividend_yield, pb) VALUES
                ('TWSE','2025-01-08','G', 6, 1.2),
                ('TWSE','2025-01-08','X', 2, 1.2);
            """);

        var rows = await Screen(db, new ScreenerCriteria { DividendYieldMin = 5, PbMax = 1.5 });

        Assert.Equal(new[] { "G" }, rows.Select(r => r.Code).ToArray());
        Assert.Equal(6, rows[0].DividendYield);
        Assert.Equal(1.2, rows[0].Pb);
    }

    // ---- 月營收 YoY（monthly_revenue 最新月）----

    [Fact]
    public async Task RevenueYoyMin_UsesLatestMonth_ExcludesNull_BoundaryInclusive()
    {
        using var db = new TestDatabase();
        foreach (var code in new[] { "R1", "R2", "R3", "R4" })
            SeedCandidate(db, code);

        db.Execute("""
            INSERT INTO monthly_revenue (market, code, year_month, yoy_pct) VALUES
                ('TWSE','R1','2025-05', 99),   -- 舊月，不應被採用
                ('TWSE','R1','2025-06', 20),   -- 最新月 → 過
                ('TWSE','R2','2025-06', 5),    -- 不過
                ('TWSE','R3','2025-06', NULL), -- NULL → 不過
                ('TWSE','R4','2025-06', 10);   -- 恰下限 → 過（>=）
            """);

        var rows = await Screen(db, new ScreenerCriteria { RevenueYoyMin = 10 });

        Assert.Equal(new[] { "R1", "R4" }, rows.Select(r => r.Code).OrderBy(c => c).ToArray());
        Assert.Equal(20, rows.Single(r => r.Code == "R1").RevenueYoy);
    }

    // ---- 組合條件、market、上限、漲跌% ----

    [Fact]
    public async Task MultipleConditions_AreCombinedWithAnd()
    {
        using var db = new TestDatabase();
        SeedCandidate(db, "M1");
        SeedCandidate(db, "M2");
        db.Execute("""
            INSERT INTO valuation (market, date, code, pe) VALUES
                ('TWSE','2025-01-08','M1', 10), ('TWSE','2025-01-08','M2', 10);
            INSERT INTO institutional (market, date, code, foreign_net) VALUES
                ('TWSE','2025-01-07','M1', 5), ('TWSE','2025-01-08','M1', 5),  -- 連 2 日
                ('TWSE','2025-01-08','M2', 5);                                 -- 僅 1 日
            """);

        // pe 兩檔都過，但連買 2 日只有 M1 過 → AND 後只剩 M1。
        var rows = await Screen(db, new ScreenerCriteria { PeMax = 15, ForeignBuyDays = 2 });

        Assert.Equal(new[] { "M1" }, rows.Select(r => r.Code).ToArray());
    }

    [Fact]
    public async Task Market_FilterScopesUniverse()
    {
        using var db = new TestDatabase();
        SeedCandidate(db, "T1", market: "TWSE");
        SeedCandidate(db, "O1", market: "TPEX");

        var twse = await Screen(db, new ScreenerCriteria { Market = "TWSE" });
        Assert.Equal(new[] { "T1" }, twse.Select(r => r.Code).ToArray());

        var tpex = await Screen(db, new ScreenerCriteria { Market = "TPEX" });
        Assert.Equal(new[] { "O1" }, tpex.Select(r => r.Code).ToArray());

        var all = await Screen(db, new ScreenerCriteria { Market = "ALL" });
        Assert.Equal(new[] { "O1", "T1" }, all.Select(r => r.Code).OrderBy(c => c).ToArray());
    }

    [Fact]
    public async Task Result_CapsAt200Rows()
    {
        using var db = new TestDatabase();
        for (var i = 0; i < 205; i++)
            SeedCandidate(db, $"C{i:D4}");

        var rows = await Screen(db, new ScreenerCriteria());

        Assert.Equal(200, rows.Count);
    }

    [Fact]
    public async Task RevenueYoy_MissingTable_ReturnsEmptyInsteadOfThrowing()
    {
        using var db = new TestDatabase();
        SeedCandidate(db, "Z");
        db.Execute("DROP TABLE monthly_revenue;"); // 模擬正式 DB 尚未建立該表

        // 需要 monthly_revenue 才能判斷 → 回空、不丟 no such table。
        var rows = await Screen(db, new ScreenerCriteria { RevenueYoyMin = 10 });
        Assert.Empty(rows);

        // 不涉及 monthly_revenue 的條件不受影響。
        var unaffected = await Screen(db, new ScreenerCriteria { PeMax = 15 });
        Assert.NotNull(unaffected);
    }

    [Fact]
    public async Task Result_ComputesChangePctFromPreviousClose()
    {
        using var db = new TestDatabase();
        SeedCandidate(db, "UP", close: 110, change: 10);   // 前收 100 → +10%
        SeedCandidate(db, "FLAT", close: 100, change: 0);  // 前收 100 → 0%

        var rows = await Screen(db, new ScreenerCriteria());
        Assert.Equal(10, rows.Single(r => r.Code == "UP").ChangePct);
        Assert.Equal(0, rows.Single(r => r.Code == "FLAT").ChangePct);
        Assert.Equal(110, rows.Single(r => r.Code == "UP").Close);
    }
}
