using StockWeb.Data;
using StockWeb.Models;

namespace StockWeb.Api;

/// <summary>
/// 持股 API 端點（§6 驗證與 400 錯誤物件慣例）。寫入僅觸及 holdings 表（§3 白名單）；
/// user_id 固定為 "0"。股數／平均成本須為 0 或正數。
/// </summary>
public static class HoldingsEndpoints
{
    public static void MapHoldingsEndpoints(this IEndpointRouteBuilder app)
    {
        // GET /api/holdings — 目前持股清單。
        app.MapGet("/api/holdings", async (IHoldingsRepository repository) =>
            Results.Ok(await repository.GetAsync()));

        // PUT /api/holdings — 新增/覆蓋單檔持股（JSON body，upsert）。
        app.MapPut("/api/holdings", async (HoldingRequest request, IHoldingsRepository repository) =>
        {
            if (!RequestValidation.TryValidateCode(request.Code, out var error))
                return Results.BadRequest(new ApiError(error!));
            if (request.Shares is not { } shares || !double.IsFinite(shares) || shares < 0)
                return Results.BadRequest(new ApiError("股數須為 0 或正數。"));
            if (request.AvgCost is not { } avgCost || !double.IsFinite(avgCost) || avgCost < 0)
                return Results.BadRequest(new ApiError("平均成本須為 0 或正數。"));

            await repository.UpsertAsync(request.Code!.Trim(), shares, avgCost);
            return Results.Ok();
        }).DisableAntiforgery();

        // DELETE /api/holdings/{code} — 移除單檔持股（改回純觀察）。
        app.MapDelete("/api/holdings/{code}", async (string code, IHoldingsRepository repository) =>
        {
            if (!RequestValidation.TryValidateCode(code, out var error))
                return Results.BadRequest(new ApiError(error!));

            var removed = await repository.RemoveAsync(code.Trim());
            return removed ? Results.Ok() : Results.NotFound(new ApiError("此代號無持股紀錄。"));
        }).DisableAntiforgery();
    }
}
