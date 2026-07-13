using StockWeb.Data;
using StockWeb.Models;

namespace StockWeb.Api;

/// <summary>條件選股 API 端點（§6 驗證與 400 錯誤物件慣例）。</summary>
public static class ScreenerEndpoints
{
    public static void MapScreenerEndpoints(this IEndpointRouteBuilder app)
    {
        // POST /api/screener — 依 body 條件（全部選填、AND 組合）回傳符合股票列，上限 200 列。
        // JSON API、無表單，故關閉 antiforgery。
        app.MapPost("/api/screener", async (ScreenerCriteria criteria, IScreenerRepository repository) =>
        {
            if (!RequestValidation.TryValidateScreener(criteria, out var error))
                return Results.BadRequest(new ApiError(error!));

            var rows = await repository.ScreenAsync(criteria);
            return Results.Ok(rows);
        }).DisableAntiforgery();
    }
}
