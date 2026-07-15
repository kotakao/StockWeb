using System.Globalization;
using System.Xml.Linq;
using StockWeb.Models;

namespace StockWeb.Services;

/// <summary>
/// Google News RSS（RSS 2.0）純解析器。輸入原始 XML，輸出至多 max 則新聞。
/// 只取 title／source（媒體名）／pubDate／link——絕不取 description（內文摘要，著作權紅線）。
/// 對畸形內容容錯：無法解析的 XML 回空集合；缺 title 或 link 的項目略過；pubDate 無法解析時
/// 顯示字串退回原字串。
/// </summary>
public static class NewsFeedParser
{
    public static IReadOnlyList<NewsItem> Parse(string? xml, int max)
    {
        if (string.IsNullOrWhiteSpace(xml))
            return Array.Empty<NewsItem>();

        XDocument doc;
        try
        {
            doc = XDocument.Parse(xml);
        }
        catch (System.Xml.XmlException)
        {
            return Array.Empty<NewsItem>();
        }

        var items = doc.Root?.Element("channel")?.Elements("item");
        if (items is null)
            return Array.Empty<NewsItem>();

        var result = new List<NewsItem>();
        foreach (var item in items)
        {
            var title = (string?)item.Element("title");
            var link = (string?)item.Element("link");
            if (string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(link))
                continue;   // 缺標題或連結的畸形項目略過

            var source = (string?)item.Element("source");
            var pubDate = (string?)item.Element("pubDate") ?? "";

            result.Add(new NewsItem(
                title.Trim(),
                string.IsNullOrWhiteSpace(source) ? null : source.Trim(),
                pubDate,
                FormatPubDate(pubDate),
                link.Trim()));

            if (result.Count >= max)
                break;
        }

        return result;
    }

    // RFC1123（如 "Wed, 15 Jul 2026 01:22:33 GMT"）→ 本地時間 yyyy-MM-dd HH:mm；無法解析時回原字串。
    private static string FormatPubDate(string pubDate)
    {
        if (DateTimeOffset.TryParse(pubDate, CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal, out var parsed))
            return parsed.ToLocalTime().ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);
        return pubDate;
    }
}
