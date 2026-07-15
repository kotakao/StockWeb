using StockWeb.Data;
using StockWeb.Models;

namespace StockWeb.Api;

/// <summary>自選股 API 端點（§6 驗證與 400 錯誤物件慣例；user_id 固定為 "0"、上限 20 檔）。</summary>
public static class WatchlistEndpoints
{
    public static void MapWatchlistEndpoints(this IEndpointRouteBuilder app)
    {
        // GET /api/watchlist — 清單組合（最新收盤/漲跌%/近 5 日法人淨額/融資變化）。
        app.MapGet("/api/watchlist", async (IWatchlistRepository repository) =>
            Results.Ok(await repository.GetAsync()));

        // GET /api/watchlist/status — 狀態板：一次組合行情/持股損益/狀態訊號與聚合列（避免 N+1）。
        app.MapGet("/api/watchlist/status", async (IWatchlistStatusRepository repository) =>
            Results.Ok(await repository.GetStatusAsync()));

        // POST /api/watchlist — 加入代號（JSON body，無表單，關閉 antiforgery）。
        app.MapPost("/api/watchlist", async (WatchlistRequest request, IWatchlistRepository repository) =>
        {
            if (!RequestValidation.TryValidateCode(request.Code, out var error))
                return Results.BadRequest(new ApiError(error!));

            var result = await repository.AddAsync(request.Code!.Trim());
            return result switch
            {
                WatchlistAddResult.Added => Results.Ok(),
                WatchlistAddResult.Exists => Results.Ok(),   // 已在清單中，視為冪等成功。
                WatchlistAddResult.Full => Results.BadRequest(new ApiError($"自選股已達上限 {WatchlistRepository.Limit} 檔。")),
                WatchlistAddResult.NotFound => Results.BadRequest(new ApiError("查無此代號（不在每日行情資料中）。")),
                _ => Results.BadRequest(new ApiError("加入自選失敗。")),
            };
        }).DisableAntiforgery();

        // DELETE /api/watchlist/{code} — 移除代號。
        app.MapDelete("/api/watchlist/{code}", async (string code, IWatchlistRepository repository) =>
        {
            if (!RequestValidation.TryValidateCode(code, out var error))
                return Results.BadRequest(new ApiError(error!));

            var removed = await repository.RemoveAsync(code.Trim());
            return removed ? Results.Ok() : Results.NotFound(new ApiError("代號不在自選股清單中。"));
        }).DisableAntiforgery();
    }
}
