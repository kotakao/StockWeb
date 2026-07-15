using Microsoft.Extensions.Caching.Memory;
using StockWeb.Models;
using StockWeb.Services;

namespace StockWeb.Api;

/// <summary>
/// 個股新聞抓取：組 Google News RSS 查詢、以 5 秒逾時的 HttpClient 抓取、交 NewsFeedParser 解析，
/// 成功結果以 IMemoryCache 快取 30 分鐘（同代號＋語言重複瀏覽不重打）。抓取失敗不快取、回原因，
/// 由呼叫端顯示於區塊而不影響頁面其他部分（§ 區塊獨立容錯）。新聞為即時外部資料，不寫入任何資料庫。
/// </summary>
public sealed class NewsService
{
    private readonly HttpClient _http;
    private readonly IMemoryCache _cache;

    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(30);
    private const int MaxItems = 10;   // 每語言最多 10 則（著作權紅線）

    public NewsService(HttpClient http, IMemoryCache cache)
    {
        _http = http;
        _cache = cache;
    }

    public async Task<NewsResult> GetNewsAsync(string code, string? name, string lang)
    {
        var cacheKey = $"news:{lang}:{code}";
        if (_cache.TryGetValue(cacheKey, out NewsResult? cached) && cached is not null)
            return cached;

        try
        {
            var xml = await _http.GetStringAsync(BuildUrl(code, name, lang));
            var result = new NewsResult(NewsFeedParser.Parse(xml, MaxItems), null);
            _cache.Set(cacheKey, result, CacheDuration);
            return result;
        }
        catch (TaskCanceledException)
        {
            return new NewsResult(Array.Empty<NewsItem>(), "新聞來源逾時（5 秒）未回應。");
        }
        catch (HttpRequestException ex)
        {
            return new NewsResult(Array.Empty<NewsItem>(), $"無法連線新聞來源：{ex.Message}");
        }
    }

    // 中文以「公司名稱 OR 代號」查詢（無名稱時退回代號）；英文以代號查詢（資料庫無英文名）。
    private static string BuildUrl(string code, string? name, string lang)
    {
        if (lang == "en")
        {
            var q = Uri.EscapeDataString(code);
            return $"https://news.google.com/rss/search?q={q}&hl=en-US&gl=US&ceid=US:en";
        }

        var query = string.IsNullOrWhiteSpace(name) ? code : $"{name} OR {code}";
        var enc = Uri.EscapeDataString(query);
        return $"https://news.google.com/rss/search?q={enc}&hl=zh-TW&gl=TW&ceid=TW:zh-Hant";
    }
}
