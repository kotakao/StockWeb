using System.Globalization;
using System.Text.RegularExpressions;

namespace StockWeb.Api;

/// <summary>
/// 共用的參數驗證 helper（§6）：股票代號 4-6 位英數、days/months 有上限、日期須為合法 YYYY-MM-DD。
/// 各方法回傳是否合法，錯誤訊息透過 out 參數帶出，供端點組成 400 錯誤物件。
/// </summary>
public static partial class RequestValidation
{
    /// <summary>days / months 類參數上限（§6）。</summary>
    public const int MaxDays = 252;
    public const int MaxMonths = 252;

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
}
