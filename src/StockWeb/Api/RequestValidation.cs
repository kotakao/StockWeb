using System.Globalization;
using System.Text.RegularExpressions;
using StockWeb.Models;

namespace StockWeb.Api;

/// <summary>
/// 共用的參數驗證 helper（§6）：股票代號 4-6 位英數、days/months 有上限、日期須為合法 YYYY-MM-DD。
/// 各方法回傳是否合法，錯誤訊息透過 out 參數帶出，供端點組成 400 錯誤物件。
/// </summary>
public static partial class RequestValidation
{
    /// <summary>days / months 類參數上限（§6；days 252、months 60）。</summary>
    public const int MaxDays = 252;
    public const int MaxMonths = 60;

    [GeneratedRegex(@"^[A-Za-z0-9]{4,6}$")]
    private static partial Regex CodePattern();

    public static bool TryValidateCode(string? code, out string? error)
    {
        if (string.IsNullOrWhiteSpace(code) || !CodePattern().IsMatch(code))
        {
            error = "股票代號格式錯誤：須為 4-6 位英數字。";
            return false;
        }
        error = null;
        return true;
    }

    public static bool TryValidateDays(int days, out string? error)
        => TryValidateRange(days, MaxDays, "days", out error);

    public static bool TryValidateMonths(int months, out string? error)
        => TryValidateRange(months, MaxMonths, "months", out error);

    private static bool TryValidateRange(int value, int max, string name, out string? error)
    {
        if (value < 1 || value > max)
        {
            error = $"{name} 須介於 1 到 {max} 之間。";
            return false;
        }
        error = null;
        return true;
    }

    /// <summary>月份參數（YYYY-MM）驗證，成功時帶出該月第一天。</summary>
    public static bool TryValidateMonth(string? month, out DateOnly monthStart, out string? error)
    {
        monthStart = default;
        if (string.IsNullOrWhiteSpace(month) ||
            !DateOnly.TryParseExact($"{month}-01", "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
        {
            error = "month 格式錯誤：須為合法的 YYYY-MM。";
            return false;
        }
        monthStart = parsed;
        error = null;
        return true;
    }

    public static bool TryValidateDate(string? date, out string? error)
    {
        if (string.IsNullOrWhiteSpace(date) ||
            !DateOnly.TryParseExact(date, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out _))
        {
            error = "日期格式錯誤：須為合法的 YYYY-MM-DD。";
            return false;
        }
        error = null;
        return true;
    }

    private static readonly HashSet<string> ValidMarkets = new(StringComparer.OrdinalIgnoreCase)
    {
        "TWSE", "TPEX", "ALL",
    };

    /// <summary>
    /// 篩選條件驗證（全部選填）：market 須為 TWSE/TPEX/ALL；連買日數 1..252；數值須為有限值，
    /// pe/pb/殖利率/量能倍數不得為負，量能倍數與 pe/pb 上限須大於 0；revenue_yoy 可為負（衰退亦為合法條件）。
    /// </summary>
    public static bool TryValidateScreener(ScreenerCriteria criteria, out string? error)
    {
        if (criteria.Market is not null && !ValidMarkets.Contains(criteria.Market.Trim()))
        {
            error = "market 須為 TWSE、TPEX 或 ALL。";
            return false;
        }
        if (!TryValidatePositiveBound(criteria.PeMax, "pe_max", out error)) return false;
        if (!TryValidateNonNegative(criteria.DividendYieldMin, "dividend_yield_min", out error)) return false;
        if (!TryValidatePositiveBound(criteria.PbMax, "pb_max", out error)) return false;
        if (!TryValidateFinite(criteria.RevenueYoyMin, "revenue_yoy_min", out error)) return false;
        if (!TryValidatePositiveBound(criteria.VolumeMultipleMin, "volume_multiple_min", out error)) return false;
        if (!TryValidateBuyDays(criteria.ForeignBuyDays, "foreign_buy_days", out error)) return false;
        if (!TryValidateBuyDays(criteria.TrustBuyDays, "trust_buy_days", out error)) return false;

        error = null;
        return true;
    }

    private static bool TryValidateBuyDays(int? value, string name, out string? error)
    {
        if (value is { } v && (v < 1 || v > MaxDays))
        {
            error = $"{name} 須介於 1 到 {MaxDays} 之間。";
            return false;
        }
        error = null;
        return true;
    }

    private static bool TryValidateFinite(double? value, string name, out string? error)
    {
        if (value is { } v && !double.IsFinite(v))
        {
            error = $"{name} 須為有限數值。";
            return false;
        }
        error = null;
        return true;
    }

    private static bool TryValidateNonNegative(double? value, string name, out string? error)
    {
        if (!TryValidateFinite(value, name, out error)) return false;
        if (value is { } v && v < 0)
        {
            error = $"{name} 不得為負。";
            return false;
        }
        error = null;
        return true;
    }

    private static bool TryValidatePositiveBound(double? value, string name, out string? error)
    {
        if (!TryValidateFinite(value, name, out error)) return false;
        if (value is { } v && v <= 0)
        {
            error = $"{name} 須大於 0。";
            return false;
        }
        error = null;
        return true;
    }
}
