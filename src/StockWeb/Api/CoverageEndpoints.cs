using StockWeb.Data;

namespace StockWeb.Api;

/// <summary>Coverage API 端點註冊。</summary>
public static class CoverageEndpoints
{
    public static void MapCoverageEndpoints(this IEndpointRouteBuilder app)
    {
        // GET /api/coverage — 各表起訖日與 distinct 日數（§6）。無參數，故此處未觸發驗證，
        // 但 RequestValidation 已為後續帶參數的端點立好基礎。
        app.MapGet("/api/coverage", async (ICoverageRepository repository) =>
        {
            var rows = await repository.GetCoverageAsync();
            return Results.Ok(rows);
        });
    }
}
