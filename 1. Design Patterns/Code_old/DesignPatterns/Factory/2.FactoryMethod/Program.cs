// Solution 2: Factory Method
//The Concept: We stop using a central "Manager" (static class) and switch to a "Franchise" model (Inheritance).
//We define an interface for creating a connection, but let specific subclasses decide which connection to make.
IDbConnectionFactory sqlDbConnectionFactory = new SqlDbConnectionFactory();
IDbCommandFactory sqlDbCommandFactory = new SqlDbCommandFactory();
DataImporter dataImporter = new DataImporter(sqlDbConnectionFactory, sqlDbCommandFactory);
dataImporter.Import();

//The following gives exception as we are tying to use pgsql command with sql server connection.
/*
NpgsqlDbCommandFactory npgsqlDbCommandFactory = new();
dataImporter = new(sqlDbConnectionFactory, npgsqlDbCommandFactory);
dataImporter.Import();
*/

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

#region CONNECTION FACTORIES
// Factory A: Makes Connections
public interface IDbConnectionFactory
{
    IDbConnection CreateDbConnection();
}

public class SqlDbConnectionFactory : IDbConnectionFactory
{
    public IDbConnection CreateDbConnection()
    {
        return new SqlConnection(connectionString: "Server:...");
    }
}

public class NpgsqlDbConnectionFactory : IDbConnectionFactory
{
    public IDbConnection CreateDbConnection()
    {
        return new NpgsqlConnection(connectionString: "Server:...");
    }
}

#endregion

#region COMMAND FACTORIES

// Factory B: Makes Commands
public interface IDbCommandFactory
{
    IDbCommand CreateDbCommand();
}

public class SqlDbCommandFactory : IDbCommandFactory
{
    public IDbCommand CreateDbCommand() => new SqlCommand();
}

public class NpgsqlDbCommandFactory : IDbCommandFactory
{
    public IDbCommand CreateDbCommand()
    {
        return new NpgsqlCommand();
    }
}
#endregion

#region CLIENT
//Client:
// We inject the specific factory (e.g., SqlFactory)
public class DataImporter
{
    private readonly IDbConnectionFactory _dbConnectionFactory; // Decoupled!
    private readonly IDbCommandFactory _dbCommandFactory; // Decoupled!

    public DataImporter(IDbConnectionFactory dbConnectionFactory, IDbCommandFactory dbCommandFactory)
    {
        _dbConnectionFactory = dbConnectionFactory;
        _dbCommandFactory = dbCommandFactory;
    }
    public void Import()
    {
        using IDbConnection connection = _dbConnectionFactory.CreateDbConnection(); // I don't know it's SQL, and I don't care.
        using IDbCommand command = _dbCommandFactory.CreateDbCommand(); // I don't know it's SQL, and I don't care.
        connection.Open();

        // CRASH HAPPENS HERE
        // The command needs a valid connection property.
        // If connection is SQL Server and command is PostgreSQL, this throws an InvalidCastException.
        command.Connection = connection;
        command.ExecuteNonQuery();
    }
}

#endregion
// Pros:
// OCP Compliant: Add OracleDbConnectionFactory.cs and OracleDbCommandFactory.cs without touching existing code.
// Clean Code: The Import() method no longer takes the providerType parameter.
// Cons:
// Oops! We mixed the families.
// services.AddTransient<IDbConnectionFactory, SqlDbConnectionFactory>();     // SQL
// services.AddTransient<IDbCommandFactory, NpgsqlDbCommandFactory>();        // Postgres
// or to visualize it without DI
// var connectionFactory = new SqlDbConnectionFactory();
// var commandFactory    = new NpgsqlDbCommandFactory();
// var dataImporter = new DataImporter(connectionFactory, commandFactory);

// We have solved the Connection problem, but our app keeps crashing because developers
// are manually creating new SqlCommand() and trying to run it on a NpgsqlConnection.
// We need to bundle them together.