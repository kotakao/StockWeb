using StockWeb.Data;
using StockWeb.Models;

namespace StockWeb.Api;

/// <summary>個股新聞端點（§6 驗證與 400 錯誤物件慣例）。新聞為即時外部資料，不寫入資料庫。</summary>
public static class NewsEndpoints
{
    public static void MapNewsEndpoints(this IEndpointRouteBuilder app)
    {
        // GET /api/stocks/{code}/news?lang=zh|en — Google News RSS（快取 30 分鐘，區塊獨立容錯）。
        app.MapGet("/api/stocks/{code}/news", async (
            string code, string? lang, NewsService news, IStockRepository stocks) =>
        {
            if (!RequestValidation.TryValidateCode(code, out var codeError))
                return Results.BadRequest(new ApiError(codeError!));
            if (!RequestValidation.TryValidateLang(lang, out var normalizedLang, out var langError))
                return Results.BadRequest(new ApiError(langError!));

            var name = await stocks.GetNameAsync(code);
            return Results.Ok(await news.GetNewsAsync(code, name, normalizedLang));
        });
    }
}
