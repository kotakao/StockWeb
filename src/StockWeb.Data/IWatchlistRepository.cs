using StockWeb.Models;

namespace StockWeb.Data;

/// <summary>加入自選股的結果（對齊 Bot add_watch 的 added/exists/full，另加代號不存在）。</summary>
public enum WatchlistAddResult
{
    Added,
    Exists,
    Full,
    NotFound,
}

/// <summary>
/// 自選股清單存取（§3 鐵律：user_id 固定為網頁保留值 "0"、上限 20 檔；
/// 寫入僅 INSERT/DELETE watchlist 表，其餘表一律唯讀）。
/// </summary>
public interface IWatchlistRepository
{
    /// <summary>清單總覽：最新收盤/漲跌%、近 5 日法人淨額、融資餘額變化（一次查詢組合）。</summary>
    Task<IReadOnlyList<WatchlistRow>> GetAsync();

    /// <summary>加入代號（須存在於 daily_quotes、未達上限、尚未加入）。</summary>
    Task<WatchlistAddResult> AddAsync(string code);

    /// <summary>移除代號；回傳是否確實刪除（原本存在）。</summary>
    Task<bool> RemoveAsync(string code);
}
