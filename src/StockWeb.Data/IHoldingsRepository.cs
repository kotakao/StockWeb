using StockWeb.Models;

namespace StockWeb.Data;

/// <summary>
/// 持股存取（§3 鐵律：holdings 為可寫白名單表，user_id 固定網頁保留值 "0"；
/// 寫入僅 upsert/DELETE holdings 一表，觸及其他表即為錯誤）。
/// </summary>
public interface IHoldingsRepository
{
    /// <summary>網頁使用者（user_id="0"）的持股清單（依代號排序）。</summary>
    Task<IReadOnlyList<Holding>> GetAsync();

    /// <summary>新增或覆蓋單檔持股（股數／平均成本），並更新 updated_at（冪等 upsert）。</summary>
    Task UpsertAsync(string code, double shares, double avgCost);

    /// <summary>移除單檔持股；回傳是否確實刪除（原本存在）。</summary>
    Task<bool> RemoveAsync(string code);
}
