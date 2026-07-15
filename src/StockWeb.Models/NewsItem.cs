namespace StockWeb.Models;

/// <summary>
/// 單則新聞（Google News RSS 的一個 item）。著作權紅線：僅保留標題、來源媒體、發布時間與外部連結，
/// 絕不含內文全文。PublishedAt 為原始 pubDate（RFC1123）；PublishedDisplay 為正規化後的顯示字串
/// （yyyy-MM-dd HH:mm，解析失敗時回原字串）。
/// </summary>
public record NewsItem(string Title, string? Source, string PublishedAt, string PublishedDisplay, string Link);

/// <summary>
/// /api/stocks/{code}/news 回應：新聞清單與（失敗時的）原因。區塊獨立容錯——外部抓取失敗時
/// Items 為空、Error 帶原因，頁面其他部分不受影響。
/// </summary>
public record NewsResult(IReadOnlyList<NewsItem> Items, string? Error);
