using StockWeb.Models;

namespace StockWeb.Data;

/// <summary>
/// 觀察名單狀態板查詢（GET /api/watchlist/status）。以少數批次查詢一次組合整份清單，避免逐檔 N+1。
/// 全程唯讀（含 holdings 讀取）；不做任何寫入。
/// </summary>
public interface IWatchlistStatusRepository
{
    Task<WatchlistStatusResponse> GetStatusAsync();
}
