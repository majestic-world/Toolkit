using System.Data.SQLite;

namespace L2Toolkit.database;

public class SqLiteConnection
{
    private const string DatabasePath = @"C:\Users\Dev\Desktop\L2Toolkit.db";
    private const string ConnectionString = $"Data Source={DatabasePath};Version=3;";

    public SQLiteConnection GetConnection()
    {
        var connection = new SQLiteConnection(ConnectionString);
        connection.Open();
        return connection;
    }
}