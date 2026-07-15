using StockWeb.Data;

namespace StockWeb.Tests;

/// <summary>
/// 法說會查詢單元測試：個股依召開日由近到遠、月界半開區間、自選股高亮旗標、
/// 說明欄解析流貫至 Detail、investor_conferences 表不存在時容忍回空。
/// </summary>
public class ConferenceRepositoryTests
{
    private static void Seed(
        TestDatabase db, string code, string announceDate, string announceTime,
        string factDate, string? description = null, string market = "TWSE")
    {
        db.Execute(
            "INSERT INTO investor_conferences " +
            "(market, code, announce_date, announce_time, name, subject, matched_clause, fact_date, description, report_date) " +
            "VALUES (@market, @code, @announceDate, @announceTime, @code, '本公司自辦法人說明會', '第12款', @factDate, @description, @announceDate);",
            new { market, code, announceDate, announceTime, factDate, description });
    }

    [Fact]
    public async Task GetByCode_OrdersByFactDateDescending()
    {
        using var db = new TestDatabase();
        Seed(db, "1539", "2026-07-13", "13:49:44", "2026-07-16");
        Seed(db, "1539", "2026-07-20", "10:00:00", "2026-08-07");
        Seed(db, "6719", "2026-07-13", "14:14:17", "2026-08-07"); // 他檔，不應混入

        var rows = await new ConferenceRepository(db.Factory).GetByCodeAsync("1539", 20);

        Assert.Equal(new[] { "2026-08-07", "2026-07-16" }, rows.Select(r => r.FactDate).ToArray());
    }

    [Fact]
    public async Task GetByCode_ParsesDescriptionIntoDetail()
    {
        using var db = new TestDatabase();
        Seed(db, "1539", "2026-07-13", "13:49:44", "2026-07-16",
            "1.召開法人說明會之日期：115/07/16\r\n2.召開法人說明會之時間：14 時 00 分 \r\n" +
            "3.召開法人說明會之地點：台中市太平區永豐路78號\r\n4.法人說明會擇要訊息：說明營收");

        var row = (await new ConferenceRepository(db.Factory).GetByCodeAsync("1539", 20)).Single();

        Assert.Equal("14:00", row.Detail.MeetingTime);
        Assert.Equal("台中市太平區永豐路78號", row.Detail.Location);
        Assert.Equal("說明營收", row.Detail.Summary);
    }

    [Fact]
    public async Task GetByMonth_BoundaryAndWatchlistFlag()
    {
        using var db = new TestDatabase();
        Seed(db, "A", "2026-07-01", "09:00:00", "2026-07-31");  // 前月最後一天 → 不含
        Seed(db, "B", "2026-07-20", "09:00:00", "2026-08-01");  // 當月第一天 → 含
        Seed(db, "C", "2026-07-25", "09:00:00", "2026-08-20");  // 當月 → 含
        Seed(db, "D", "2026-08-25", "09:00:00", "2026-09-01");  // 次月第一天 → 不含
        db.Execute("INSERT INTO watchlist (user_id, code) VALUES ('0', 'C');");

        var rows = await new ConferenceRepository(db.Factory).GetByMonthAsync(new DateOnly(2026, 8, 1));

        Assert.Equal(new[] { "B", "C" }, rows.Select(r => r.Code).ToArray());
        Assert.False(rows.Single(r => r.Code == "B").InWatchlist);
        Assert.True(rows.Single(r => r.Code == "C").InWatchlist);
    }

    [Fact]
    public async Task GetByCode_MissingTable_ReturnsEmpty()
    {
        using var db = new TestDatabase();
        db.Execute("DROP TABLE investor_conferences;"); // 模擬正式 DB 尚未建立該表

        var rows = await new ConferenceRepository(db.Factory).GetByCodeAsync("1539", 20);

        Assert.Empty(rows);
    }

    [Fact]
    public async Task GetByMonth_MissingTable_ReturnsEmpty()
    {
        using var db = new TestDatabase();
        db.Execute("DROP TABLE investor_conferences;");

        var rows = await new ConferenceRepository(db.Factory).GetByMonthAsync(new DateOnly(2026, 8, 1));

        Assert.Empty(rows);
    }
}
