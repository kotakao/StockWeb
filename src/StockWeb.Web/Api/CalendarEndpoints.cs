using StockWeb.Data;
using StockWeb.Models;

namespace StockWeb.Api;

/// <summary>除權息行事曆 API 端點（§6 驗證與 400 錯誤物件慣例）。</summary>
public static class CalendarEndpoints
{
    public static void MapCalendarEndpoints(this IEndpointRouteBuilder app)
    {
        // GET /api/calendar/dividends?month=YYYY-MM — 該月除權息事件（含自選股高亮旗標）。
        app.MapGet("/api/calendar/dividends", async (ICalendarRepository repository, string? month) =>
        {
            if (!RequestValidation.TryValidateMonth(month, out var monthStart, out var error))
                return Results.BadRequest(new ApiError(error!));

            return Results.Ok(await repository.GetDividendsAsync(monthStart));
        });
    }
}
