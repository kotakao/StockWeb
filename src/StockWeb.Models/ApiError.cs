namespace StockWeb.Models;

/// <summary>
/// API 錯誤回應的統一格式（§6：錯誤一律回 400 + 訊息物件，不回 500）。
/// </summary>
public record ApiError(string Error);
