using StockWeb.Services;

namespace StockWeb.Tests;

/// <summary>
/// 法說會「說明」欄解析器單元測試。輸入取自實際重大訊息說明欄（結構化逐行文字），
/// 涵蓋實體/線上地點、時間正規化、缺值與非結構化文字（解析失敗回全 null）。
/// </summary>
public class ConferenceParserTests
{
    // 實際 TWSE 重大訊息說明欄（巨庭 1539，實體地點）。
    private const string PhysicalVenue =
        "符合條款第四條第XX款：12\r\n事實發生日：115/07/16\r\n" +
        "1.召開法人說明會之日期：115/07/16\r\n2.召開法人說明會之時間：14 時 00 分 \r\n" +
        "3.召開法人說明會之地點：台中市太平區永豐路78號\r\n" +
        "4.法人說明會擇要訊息：說明本公司最近年度營收及淨利\r\n5.其他應敘明事項：無\r\n" +
        "完整財務業務資訊請至公開資訊觀測站之法人說明會一覽表或法說會項目下查閱。";

    // 實際 TPEX 重大訊息說明欄（環球晶 6488，線上）。
    private const string OnlineVenue =
        "符合條款第四條第XX款：12\r\n事實發生日：115/07/20\r\n" +
        "1.召開法人說明會之日期：115/07/20\r\n2.召開法人說明會之時間：14 時 00 分 \r\n" +
        "3.召開法人說明會之地點：線上\r\n" +
        "4.法人說明會擇要訊息：本公司受邀參加線上法人說明會\r\n5.其他應敘明事項：無";

    [Fact]
    public void Parse_PhysicalVenue_ExtractsTimeLocationSummary()
    {
        var d = ConferenceParser.Parse(PhysicalVenue);

        Assert.Equal("14:00", d.MeetingTime);
        Assert.Equal("台中市太平區永豐路78號", d.Location);
        Assert.Equal("說明本公司最近年度營收及淨利", d.Summary);
    }

    [Fact]
    public void Parse_OnlineVenue_LocationIsOnline()
    {
        var d = ConferenceParser.Parse(OnlineVenue);

        Assert.Equal("14:00", d.MeetingTime);
        Assert.Equal("線上", d.Location);
        Assert.Equal("本公司受邀參加線上法人說明會", d.Summary);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Parse_Empty_ReturnsAllNull(string? input)
    {
        var d = ConferenceParser.Parse(input);
        Assert.Null(d.MeetingTime);
        Assert.Null(d.Location);
        Assert.Null(d.Summary);
    }

    [Fact]
    public void Parse_UnstructuredText_ReturnsAllNull()
    {
        var d = ConferenceParser.Parse("本公司訂於近日召開法人說明會，詳情容後公布。");
        Assert.Null(d.MeetingTime);
        Assert.Null(d.Location);
        Assert.Null(d.Summary);
    }

    [Fact]
    public void Parse_UnrecognizedTimeFormat_FallsBackToTrimmedValue()
    {
        var d = ConferenceParser.Parse("2.召開法人說明會之時間：下午兩點");
        Assert.Equal("下午兩點", d.MeetingTime);
    }
}
