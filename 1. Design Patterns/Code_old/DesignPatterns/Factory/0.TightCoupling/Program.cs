//The Scenario: You write an app that connects to SQL Server. You hardcode the classes.
DataImporter dataImporter = new DataImporter();
dataImporter.Import();


public class SqlConnection : IDisposable
{
    private readonly string _connectionString;
    public SqlConnection(string connectionString)
    {
        _connectionString = connectionString;
    }
    public void Open()
    {
        Console.WriteLine($"[SQL Server] Connection Opened successfully at {_connectionString}");
    }

    public void Dispose()
    {
        Console.WriteLine($"[SQL Server] Connection Closed successfully");
    }
}

public class SqlCommand : IDisposable
{
    public SqlConnection? Connection { get; set; }
    public void ExecuteNonQuery()
    {
        Console.WriteLine("[SQL Server] Executing T-SQL Command...");
    }
    public void Dispose() { }
}

public class DataImporter
{
    public void Import()
    {
        // Tight Coupling: You are "married" to SQL Server
        SqlConnection connection = new SqlConnection("Server=...");
        SqlCommand command = new SqlCommand();

        connection.Open();
        command.Connection = connection;
        command.ExecuteNonQuery();
    }
}

