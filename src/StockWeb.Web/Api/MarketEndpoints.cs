using StockWeb.Data;
using StockWeb.Models;
using StockWeb.Services;

namespace StockWeb.Api;

/// <summary>市場儀表板 API 端點（§6 驗證與 400 錯誤物件慣例；days 未帶時預設 60）。</summary>
public static class MarketEndpoints
{
    private const int DefaultDays = 60;

    public static void MapMarketEndpoints(this IEndpointRouteBuilder app)
    {
        // GET /api/market/daily?days= — 大盤日線序列（日期由舊到新，供圖表時間軸使用）。
        app.MapGet("/api/market/daily", async (IMarketRepository repository, int days = DefaultDays) =>
        {
            if (!RequestValidation.TryValidateDays(days, out var error))
                return Results.BadRequest(new ApiError(error!));

            var rows = await repository.GetDailyAsync(days);
            return Results.Ok(Enumerable.Reverse(rows).ToList());
        });

        // GET /api/market/breadth?days= — A/D Line、量能溫度、三大法人近 N 日累積（伺服器端計算）。
        app.MapGet("/api/market/breadth", async (IMarketRepository repository, int days = DefaultDays) =>
        {
            if (!RequestValidation.TryValidateDays(days, out var error))
                return Results.BadRequest(new ApiError(error!));

            var rows = await repository.GetDailyAsync(days);
            var response = new BreadthResponse(
                MarketBreadthCalculator.AdvanceDeclineLine(rows),
                MarketBreadthCalculator.VolumeTemperature(rows),
                MarketBreadthCalculator.ForeignFlowTrend(rows),
                MarketBreadthCalculator.TrustFlowTrend(rows),
                MarketBreadthCalculator.DealerFlowTrend(rows));
            return Results.Ok(response);
        });
    }
}
