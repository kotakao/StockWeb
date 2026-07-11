using Microsoft.Data.Sqlite;

namespace StockWeb.Data;

/// <summary>
/// SQLite 連線工廠。市場資料表一律走唯讀連線；唯一可寫的表是 watchlist，走讀寫連線。
/// （§3 鐵律：StockWeb 對 market 資料表唯讀，Schema 由 Python 端獨占管理。）
/// </summary>
public interface IDbConnectionFactory
{
    /// <summary>唯讀連線（Mode=ReadOnly），供所有查詢使用。</summary>
    SqliteConnection CreateReadOnly();

    /// <summary>讀寫連線（busy_timeout=5000），僅供 watchlist 寫入使用。</summary>
    SqliteConnection CreateReadWrite();
}
