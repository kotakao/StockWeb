using Dapper;
using Microsoft.Data.Sqlite;

namespace StockWeb.Tests;

public class SqliteConnectionFactoryTests
{
    [Fact]
    public void CreateReadOnly_RejectsWrites()
    {
        using var db = new TestDatabase();

        using var connection = db.Factory.CreateReadOnly();
        var ex = Assert.Throws<SqliteException>(() =>
            connection.Execute("INSERT INTO watchlist (user_id, code) VALUES ('0', '2330');"));

        // SQLITE_READONLY = 8
        Assert.Equal(8, ex.SqliteErrorCode);
    }

    [Fact]
    public void CreateReadWrite_SetsBusyTimeoutTo5000()
    {
        using var db = new TestDatabase();

        using var connection = db.Factory.CreateReadWrite();
        var busyTimeout = connection.ExecuteScalar<long>("PRAGMA busy_timeout;");

        Assert.Equal(5000, busyTimeout);
    }

    [Fact]
    public void CreateReadWrite_AllowsWatchlistWrite()
    {
        using var db = new TestDatabase();

        using (var connection = db.Factory.CreateReadWrite())
        {
            connection.Execute("INSERT INTO watchlist (user_id, code) VALUES ('0', '2330');");
        }

        using var readOnly = db.Factory.CreateReadOnly();
        var count = readOnly.ExecuteScalar<long>("SELECT COUNT(*) FROM watchlist;");
        Assert.Equal(1, count);
    }
}
