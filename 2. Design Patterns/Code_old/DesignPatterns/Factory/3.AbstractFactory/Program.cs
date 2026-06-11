// Solution 3: Abstract Factory
// The Abstract Factory acknowledges that Connection and Command are married.
// You cannot create one without implicitly agreeing to the specific implementation of the other.
// We stop creating single objects. We create Families of related objects.
// In other words we define a Single Factory that creates the Family.
// The Factory interface expands to include all parts of the ecosystem (Connection + Command + Transaction, etc.).
IDatabaseFactory databaseFactory = new SqlDatabaseFactory();
DataImporter dataImporter = new DataImporter(databaseFactory);
dataImporter.Import();


// To support multiple types, create a base type and
// inherit the concrete types from base type.
#region BASE_TYPES

// Represents a connection to a database
public interface IDbConnection : IDisposable
{
    void Open();
}
// Represents an instruction to execute against a database
public interface IDbCommand : IDisposable
{
    // The Command needs a reference to the Connection to know where to run
    IDbConnection Connection { get; set; }
    void ExecuteNonQuery();
}
#endregion

#region SQLSERVER
/*SQL SERVER IMPLEMENTATION*/
public class SqlConnection : IDbConnection
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

public class SqlCommand : IDbCommand
{
    public IDbConnection? Connection { get; set; }
    public void ExecuteNonQuery()
    {
        if (Connection is not SqlConnection)
        {
            throw new InvalidOperationException("CRITICAL ERROR: specific SqlCommand requires a specific SqlConnection!");
        }
        Console.WriteLine("[SQL Server] Executing T-SQL Command...");
    }
    public void Dispose() { }
}

#endregion

#region POSTGRES
/* POSTGRES IMPLEMENTATION */
public class NpgsqlConnection : IDbConnection
{
    private readonly string _connectionString;
    public NpgsqlConnection(string connectionString)
    {
        _connectionString = connectionString;
    }
    public void Open()
    {
        Console.WriteLine($"[PSQL] Connection Opened successfully at {_connectionString}");
    }
    public void Dispose()
    {
        Console.WriteLine($"[PSQL] Connection Closed successfully");
    }
}

public class NpgsqlCommand : IDbCommand
{
    public IDbConnection? Connection { get; set; }
    public void ExecuteNonQuery()
    {
        if (Connection is not NpgsqlConnection)
        {
            throw new InvalidOperationException("CRITICAL ERROR: specific NpgsqlCommand requires a specific NpgsqlConnection!");
        }
        Console.WriteLine("[PSQL] Executing PSQL Command...");
    }
    public void Dispose() { }
}

#endregion

#region ABSTRACT_FACTORY
//The abstract Factory holding the family of Creation methods
public interface IDatabaseFactory
{
    IDbConnection CreateDbConnection();
    IDbCommand CreateDbCommand();
}

//SqlServer Family
public class SqlDatabaseFactory : IDatabaseFactory
{
    public IDbConnection CreateDbConnection() => new SqlConnection("Server = ");
    public IDbCommand CreateDbCommand() => new SqlCommand();
}

//Postgresql Family
public class NpgsqlDatabseFactory : IDatabaseFactory
{
    public IDbConnection CreateDbConnection() => new NpgsqlConnection("Server = ");
    public IDbCommand CreateDbCommand() => new NpgsqlCommand();
}

#endregion

#region CLIENT

//Client:
// We inject the specific factory (e.g., SqlFactory)
public class DataImporter
{
    private readonly IDatabaseFactory _databaseFactory;
    public DataImporter(IDatabaseFactory databaseFactory)
    {
        _databaseFactory = databaseFactory;
    }

    public void Import()
    {
        // 1. Create Connection
        using var connnection = _databaseFactory.CreateDbConnection();
        // 2. Create Command
        // GUARANTEED to match the connection type because it came from the SAME factory instance.
        using var command = _databaseFactory.CreateDbCommand();

        connnection.Open();
        // This is now 100% safe. 
        // We know for a fact that if connection is SQL, command is SQL.
        command.Connection = connnection;
        command.ExecuteNonQuery();
    }
}
#endregion

//Factory Method: Good for Single objects. (e.g., "I just need a Logger").
//Abstract Factory: Mandatory for Interdependent objects. (e.g., "I need a Connection and a Command that must talk to each other").