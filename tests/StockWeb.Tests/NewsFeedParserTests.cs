using System.Text.RegularExpressions;
using StockWeb.Services;

namespace StockWeb.Tests;

/// <summary>
/// Google News RSS 解析器單元測試，以錄製的 XML fixture 為輸入（正常／空結果／畸形項目），
/// 涵蓋 max 上限、缺欄位項目略過、pubDate 畸形退回原字串、非 XML 容錯。
/// </summary>
public class NewsFeedParserTests
{
    private static string Fixture(string name)
        => File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", name));

    [Fact]
    public void Parse_RecordedFeed_ExtractsTitleSourcePubDateLink()
    {
        var items = NewsFeedParser.Parse(Fixture("news_zh_recorded.xml"), 10);

        Assert.Equal(3, items.Count);
        var first = items[0];
        Assert.Equal("台積電法說會前夕！盤中漲15元至2435元 分析師：買黑不買紅 - Yahoo股市", first.Title);
        Assert.Equal("Yahoo股市", first.Source);
        Assert.StartsWith("https://news.google.com/rss/articles/", first.Link);
        Assert.Equal("Wed, 15 Jul 2026 01:22:33 GMT", first.PublishedAt);
        // 顯示字串正規化為 yyyy-MM-dd HH:mm（本地時區，故只驗格式不驗絕對值）。
        Assert.Matches(new Regex(@"^\d{4}-\d{2}-\d{2} \d{2}:\d{2}$"), first.PublishedDisplay);
    }

    [Fact]
    public void Parse_RespectsMaxLimit()
    {
        var items = NewsFeedParser.Parse(Fixture("news_zh_recorded.xml"), 2);
        Assert.Equal(2, items.Count);
    }

    [Fact]
    public void Parse_EmptyFeed_ReturnsEmpty()
    {
        Assert.Empty(NewsFeedParser.Parse(Fixture("news_empty.xml"), 10));
    }

    [Fact]
    public void Parse_MalformedItems_SkipsIncomplete_KeepsValid()
    {
        var items = NewsFeedParser.Parse(Fixture("news_malformed.xml"), 10);

        // 缺 title 與缺 link 的兩個項目略過，保留 2 個有效項目。
        Assert.Equal(2, items.Count);

        var badDate = items[0];
        Assert.Null(badDate.Source);                       // 無 source → null
        Assert.Equal("不是日期", badDate.PublishedDisplay); // 日期無法解析 → 顯示退回原字串

        Assert.Equal("好媒體", items[1].Source);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("<not-xml")]
    [InlineData("plain text, not xml at all")]
    public void Parse_InvalidInput_ReturnsEmpty(string? input)
    {
        Assert.Empty(NewsFeedParser.Parse(input, 10));
    }
}
