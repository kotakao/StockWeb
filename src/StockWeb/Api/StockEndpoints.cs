using StockWeb.Data;
using StockWeb.Models;

namespace StockWeb.Api;

/// <summary>個股頁六端點（§6 驗證與 400 錯誤物件慣例）。序列一律回日期由舊到新。</summary>
public static class StockEndpoints
{
    private const int DefaultQuoteDays = 252;   // K 線／估值預設近一年交易日
    private const int DefaultFlowDays = 60;     // 法人／融資券預設近 60 交易日
    private const int DefaultRevenueMonths = 24;
    private const int DefaultFinancialsQuarters = 20;

    public static void MapStockEndpoints(this IEndpointRouteBuilder app)
    {
        // GET /api/stocks/{code}/quotes?days=&adjusted=&period= — OHLCV，adjusted 控制前復權，
        // period（daily/weekly/monthly/yearly）控制聚合週期。
        app.MapGet("/api/stocks/{code}/quotes", async (
            string code, IStockRepository repository,
            int days = DefaultQuoteDays, bool adjusted = true, string period = "daily") =>
        {
            if (!TryValidate(code, days, RequestValidation.TryValidateDays, out var error))
                return Results.BadRequest(error);
            if (!RequestValidation.TryValidatePeriod(period, out var quotePeriod, out var periodError))
                return Results.BadRequest(new ApiError(periodError!));

            return Results.Ok(await repository.GetQuotesAsync(code, days, adjusted, quotePeriod));
        });

        // GET /api/stocks/{code}/institutional?days= — 逐日三大法人。
        app.MapGet("/api/stocks/{code}/institutional", async (
            string code, IStockRepository repository, int days = DefaultFlowDays) =>
        {
            if (!TryValidate(code, days, RequestValidation.TryValidateDays, out var error))
                return Results.BadRequest(error);

            return Results.Ok(await repository.GetInstitutionalAsync(code, days));
        });

        // GET /api/stocks/{code}/margin?days= — 逐日融資券餘額。
        app.MapGet("/api/stocks/{code}/margin", async (
            string code, IStockRepository repository, int days = DefaultFlowDays) =>
        {
            if (!TryValidate(code, days, RequestValidation.TryValidateDays, out var error))
                return Results.BadRequest(error);

            return Results.Ok(await repository.GetMarginAsync(code, days));
        });

        // GET /api/stocks/{code}/valuation?days= — 估值歷史。
        app.MapGet("/api/stocks/{code}/valuation", async (
            string code, IStockRepository repository, int days = DefaultQuoteDays) =>
        {
            if (!TryValidate(code, days, RequestValidation.TryValidateDays, out var error))
                return Results.BadRequest(error);

            return Results.Ok(await repository.GetValuationAsync(code, days));
        });

        // GET /api/stocks/{code}/revenue?months= — 月營收與 YoY。
        app.MapGet("/api/stocks/{code}/revenue", async (
            string code, IStockRepository repository, int months = DefaultRevenueMonths) =>
        {
            if (!TryValidate(code, months, RequestValidation.TryValidateMonths, out var error))
                return Results.BadRequest(error);

            return Results.Ok(await repository.GetRevenueAsync(code, months));
        });

        // GET /api/stocks/{code}/dividends — 除權息事件（無數量參數）。
        app.MapGet("/api/stocks/{code}/dividends", async (string code, IStockRepository repository) =>
        {
            if (!RequestValidation.TryValidateCode(code, out var codeError))
                return Results.BadRequest(new ApiError(codeError!));

            return Results.Ok(await repository.GetDividendsAsync(code));
        });

        // GET /api/stocks/{code}/financials?quarters= — 季度損益（quarters 上限 40）。
        app.MapGet("/api/stocks/{code}/financials", async (
            string code, IStockRepository repository, int quarters = DefaultFinancialsQuarters) =>
        {
            if (!TryValidate(code, quarters, RequestValidation.TryValidateQuarters, out var error))
                return Results.BadRequest(error);

            return Results.Ok(await repository.GetFinancialsAsync(code, quarters));
        });
    }

    // 個股端點共用：先驗代號、再驗數量參數（days 或 months），任一失敗即帶出 400 錯誤物件。
    private delegate bool RangeValidator(int value, out string? error);

    private static bool TryValidate(string code, int value, RangeValidator validator, out ApiError? error)
    {
        if (!RequestValidation.TryValidateCode(code, out var codeError))
        {
            error = new ApiError(codeError!);
            return false;
        }
        if (!validator(value, out var rangeError))
        {
            error = new ApiError(rangeError!);
            return false;
        }
        error = null;
        return true;
    }
}
