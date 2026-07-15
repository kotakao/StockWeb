using StockWeb.Data;
using StockWeb.Models;

namespace StockWeb.Api;

/// <summary>法說會端點（§6 驗證與 400 錯誤物件慣例；investor_conferences 唯讀）。</summary>
public static class ConferenceEndpoints
{
    private const int DefaultCodeLimit = 20;   // 個股頁近期＋未來場次上限

    public static void MapConferenceEndpoints(this IEndpointRouteBuilder app)
    {
        // GET /api/stocks/{code}/conferences — 該公司近期與未來法說會（依召開日由近到遠）。
        app.MapGet("/api/stocks/{code}/conferences", async (string code, IConferenceRepository repository) =>
        {
            if (!RequestValidation.TryValidateCode(code, out var error))
                return Results.BadRequest(new ApiError(error!));

            return Results.Ok(await repository.GetByCodeAsync(code, DefaultCodeLimit));
        });

        // GET /api/calendar/conferences?month=YYYY-MM — 該月全市場法說會（依召開日，含自選股高亮旗標）。
        app.MapGet("/api/calendar/conferences", async (string? month, IConferenceRepository repository) =>
        {
            if (!RequestValidation.TryValidateMonth(month, out var monthStart, out var error))
                return Results.BadRequest(new ApiError(error!));

            return Results.Ok(await repository.GetByMonthAsync(monthStart));
        });
    }
}
