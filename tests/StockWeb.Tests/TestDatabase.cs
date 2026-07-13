using Dapper;
using Microsoft.Data.Sqlite;
using StockWeb.Data;

namespace StockWeb.Tests;

/// <summary>
/// 測試用暫時 SQLite 檔＋最小 schema。schema 抄自 StockDCbot storage.py 的 CREATE TABLE，
/// 僅供測試建立資料使用；正式程式仍嚴禁任何建表或 DDL（§3 鐵律）。
/// </summary>
public sealed class TestDatabase : IDisposable
{
    private readonly string _path;

    public IDbConnectionFactory Factory { get; }

    // 僅測試用 DDL。欄位定義與 storage.py 一致，涵蓋 coverage 查詢用到的 7 張表與 watchlist。
    private const string Schema = """
        CREATE TABLE daily_quotes (
            market TEXT NOT NULL DEFAULT 'TWSE', date TEXT NOT NULL, code TEXT NOT NULL,
            name TEXT, open REAL, high REAL, low REAL, close REAL, change REAL, volume REAL,
            PRIMARY KEY (market, date, code)
        );
        CREATE TABLE institutional (
            market TEXT NOT NULL DEFAULT 'TWSE', date TEXT NOT NULL, code TEXT NOT NULL,
            name TEXT, foreign_net REAL, trust_net REAL, dealer_net REAL,
            PRIMARY KEY (market, date, code)
        );
        CREATE TABLE margin (
            market TEXT NOT NULL DEFAULT 'TWSE', date TEXT NOT NULL, code TEXT NOT NULL,
            name TEXT, margin_balance REAL, margin_prev REAL, short_balance REAL, short_prev REAL,
            PRIMARY KEY (market, date, code)
        );
        CREATE TABLE valuation (
            market TEXT NOT NULL DEFAULT 'TWSE', date TEXT NOT NULL, code TEXT NOT NULL,
            pe REAL, dividend_yield REAL, pb REAL,
            PRIMARY KEY (market, date, code)
        );
        CREATE TABLE market_daily (
            market TEXT NOT NULL DEFAULT 'TWSE', date TEXT NOT NULL, index_close REAL,
            index_change_pct REAL, turnover REAL, up_count REAL, down_count REAL,
            foreign_net REAL, trust_net REAL, dealer_net REAL, margin_balance REAL, short_balance REAL,
            PRIMARY KEY (market, date)
        );
        CREATE TABLE dividend_events (
            market TEXT NOT NULL DEFAULT 'TWSE', code TEXT NOT NULL, ex_date TEXT NOT NULL,
            name TEXT, event_type TEXT, cash_dividend REAL, stock_ratio REAL,
            PRIMARY KEY (market, code, ex_date)
        );
        CREATE TABLE monthly_revenue (
            market TEXT NOT NULL DEFAULT 'TWSE', code TEXT NOT NULL, year_month TEXT NOT NULL,
            name TEXT, revenue REAL, mom_pct REAL, yoy_pct REAL, cum_revenue REAL, cum_yoy_pct REAL,
            PRIMARY KEY (market, code, year_month)
        );
        CREATE TABLE watchlist (
            user_id TEXT NOT NULL, code TEXT NOT NULL,
            PRIMARY KEY (user_id, code)
        );
        CREATE TABLE quarterly_financials (
            market TEXT NOT NULL DEFAULT 'TWSE', code TEXT NOT NULL, year_quarter TEXT NOT NULL,
            name TEXT, revenue REAL, gross_profit REAL, operating_income REAL, net_income REAL, eps REAL,
            PRIMARY KEY (market, code, year_quarter)
        );
        """;

    public TestDatabase()
    {
        _path = Path.Combine(Path.GetTempPath(), $"stockweb_test_{Guid.NewGuid():N}.db");

        var createConnectionString = new SqliteConnectionStringBuilder
        {
            DataSource = _path,
            Mode = SqliteOpenMode.ReadWriteCreate,
        }.ToString();

        using (var connection = new SqliteConnection(createConnectionString))
        {
            connection.Open();
            // 比照正式 DB（Bot 端開啟 WAL）：讀寫互不阻塞，並行寫入靠 busy_timeout 等待而非立即 SQLITE_BUSY。
            connection.Execute("PRAGMA journal_mode=WAL;");
            connection.Execute(Schema);
        }

        Factory = new SqliteConnectionFactory(_path);
    }

    /// <summary>以讀寫連線執行任意 SQL 播種測試資料。</summary>
    public void Execute(string sql, object? param = null)
    {
        using var connection = Factory.CreateReadWrite();
        connection.Execute(sql, param);
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        try
        {
            File.Delete(_path);
        }
        catch (IOException)
        {
            // 檔案殘留於暫存目錄不影響測試結果，忽略。
        }
    }
}
