using Microsoft.Data.Sqlite;

namespace StockWeb.Data;

/// <summary>
/// 依 appsettings.json 的 DbPath 建立 SQLite 連線。
/// 讀寫連線在開啟後設定 busy_timeout=5000，避開 Bot 17:00 寫入窗口的鎖競爭（§3）。
/// </summary>
public sealed class SqliteConnectionFactory : IDbConnectionFactory
{
    private readonly string _readOnlyConnectionString;
    private readonly string _readWriteConnectionString;

    public SqliteConnectionFactory(string dbPath)
    {
        if (string.IsNullOrWhiteSpace(dbPath))
            throw new ArgumentException("DbPath 未設定。", nameof(dbPath));

        _readOnlyConnectionString = new SqliteConnectionStringBuilder
        {
            DataSource = dbPath,
            Mode = SqliteOpenMode.ReadOnly,
        }.ToString();

        _readWriteConnectionString = new SqliteConnectionStringBuilder
        {
            DataSource = dbPath,
            Mode = SqliteOpenMode.ReadWrite,
        }.ToString();
    }

    public SqliteConnection CreateReadOnly()
    {
        var connection = new SqliteConnection(_readOnlyConnectionString);
        connection.Open();
        return connection;
    }

    public SqliteConnection CreateReadWrite()
    {
        var connection = new SqliteConnection(_readWriteConnectionString);
        connection.Open();
        using var pragma = connection.CreateCommand();
        pragma.CommandText = "PRAGMA busy_timeout=5000;";
        pragma.ExecuteNonQuery();
        return connection;
    }
}
