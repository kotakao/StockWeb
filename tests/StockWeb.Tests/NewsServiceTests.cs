using System.Net;
using Microsoft.Extensions.Caching.Memory;
using StockWeb.Api;

namespace StockWeb.Tests;

/// <summary>
/// NewsService 快取與容錯行為測試（以攔截 HttpClient 的 stub handler 計數）：
/// 同代號＋語言重複查詢只打一次外部、不同語言各自打、查詢字串依語言組字、
/// 失敗不快取且回原因、逾時回逾時原因。
/// </summary>
public class NewsServiceTests
{
    private const string OkXml =
        "<?xml version=\"1.0\"?><rss version=\"2.0\"><channel>" +
        "<item><title>標題</title><link>https://news.example/1</link>" +
        "<pubDate>Wed, 15 Jul 2026 01:22:33 GMT</pubDate>" +
        "<source url=\"https://s\">某媒體</source></item></channel></rss>";

    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _responder;
        public int CallCount { get; private set; }
        public List<string> Urls { get; } = new();

        public StubHandler(Func<HttpRequestMessage, HttpResponseMessage> responder) => _responder = responder;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            CallCount++;
            Urls.Add(request.RequestUri!.ToString());
            return Task.FromResult(_responder(request));   // responder 可丟例外模擬失敗
        }
    }

    private static HttpResponseMessage Ok() =>
        new(HttpStatusCode.OK) { Content = new StringContent(OkXml) };

    private static NewsService Build(StubHandler handler) =>
        new(new HttpClient(handler), new MemoryCache(new MemoryCacheOptions()));

    [Fact]
    public async Task SameCodeAndLang_FetchesOnce()
    {
        var handler = new StubHandler(_ => Ok());
        var svc = Build(handler);

        var r1 = await svc.GetNewsAsync("2330", "台積電", "zh");
        var r2 = await svc.GetNewsAsync("2330", "台積電", "zh");

        Assert.Equal(1, handler.CallCount);   // 第二次命中快取，不重打
        Assert.Single(r1.Items);
        Assert.Null(r1.Error);
        Assert.Same(r1, r2);
    }

    [Fact]
    public async Task DifferentLang_FetchesSeparately_WithLangSpecificQuery()
    {
        var handler = new StubHandler(_ => Ok());
        var svc = Build(handler);

        await svc.GetNewsAsync("2330", "台積電", "zh");
        await svc.GetNewsAsync("2330", "台積電", "en");

        Assert.Equal(2, handler.CallCount);
        Assert.Contains(handler.Urls, u => u.Contains("hl=zh-TW") && u.Contains("OR"));   // 中文：名稱 OR 代號
        Assert.Contains(handler.Urls, u => u.Contains("hl=en-US"));                        // 英文：獨立查詢
    }

    [Fact]
    public async Task Failure_NotCached_AndReportsReason()
    {
        // 第一次失敗、第二次成功（閉包變數控制）：證明失敗不寫入快取。
        var shouldFail = true;
        var handler = new StubHandler(_ => shouldFail ? throw new HttpRequestException("boom") : Ok());
        var svc = Build(handler);

        var failed = await svc.GetNewsAsync("2330", "台積電", "zh");
        Assert.Empty(failed.Items);
        Assert.NotNull(failed.Error);

        shouldFail = false;
        var ok = await svc.GetNewsAsync("2330", "台積電", "zh");
        Assert.Equal(2, handler.CallCount);   // 失敗未快取，第二次仍打外部
        Assert.Single(ok.Items);
        Assert.Null(ok.Error);
    }

    [Fact]
    public async Task Timeout_ReportsTimeoutReason()
    {
        var handler = new StubHandler(_ => throw new TaskCanceledException("timeout"));
        var svc = Build(handler);

        var result = await svc.GetNewsAsync("2330", "台積電", "zh");

        Assert.Empty(result.Items);
        Assert.Contains("逾時", result.Error);
    }
}
