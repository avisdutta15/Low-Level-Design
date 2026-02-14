DataImporter_SimpleFactoryUsed dataImporter = new DataImporter_SimpleFactoryUsed();
dataImporter.Import("postgres");


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
    public void ExecuteNonQuery() { 
        if(Connection is not SqlConnection)
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

//Problem: If you want to support PostgreSQL, you have to rewrite the DataImporter class with if-else logic everywhere.
//1. It violates the SRP for DataImporter. It should just run the commands and not create objects.
//2. It violates the OCP. To add new provider, we will have to open up the DataImporter 
public class DataImporter_Problematic
{
    public void Import(string providerType)
    {
        IDbConnection connection = null;
        IDbCommand command = null;
        if (providerType == "sql server")
        {
            // Tight Coupling: You are "married" to SQL Server
            connection = new SqlConnection("Server=...");
            command = new SqlCommand();
        }
        else if (providerType == "postgres")
        {
            connection = new NpgsqlConnection("Server=...");
            command = new NpgsqlCommand();
        }
        connection.Open();
        command.Connection = connection;
        command.ExecuteNonQuery();
    }
}

//Solution 1: Simple Factory
//Take out the object creation responsibility and dump it to a central class called as DataFactory.
//The Client asks for a database by name, and the Factory handles the switch statement.

#region SIMPLE_FACTORY
public class DatabaseProviderFactory
{
    //Create will be a static method as we donot want to create the object of the factory
    //to get the concrete objects
    public static  IDbConnection CreateDbConnection(string providerType)
    {
        return providerType switch
        {
            "sql server" => new SqlConnection(connectionString: "Server:..."),
            "postgres" => new NpgsqlConnection(connectionString: "Server:..."),
            _ => throw new Exception("Provider not supported")
        };
    }

    public static IDbCommand CreateDbCommand(string providerType)
    {
        return providerType switch
        {
            "sql server" => new SqlCommand(),
            "postgres" => new NpgsqlCommand(),
            _ => throw new Exception("Provider not supported")
        };
    }
}
#endregion

#region CLIENT
// Client: 
public class DataImporter_SimpleFactoryUsed
{
    public void Import(string providerType)
    {
        using IDbConnection connection = DatabaseProviderFactory.CreateDbConnection(providerType);
        using IDbCommand command = DatabaseProviderFactory.CreateDbCommand(providerType);
        connection.Open();
        command.Connection = connection;
        command.ExecuteNonQuery();
    }
}

#endregion

//Pros:
//Centralization: All new keywords are in one place.
//Simplicity: Very easy to write and understand.
//Cons:
//Violates OCP: To add "Oracle", you must open and modify the Factory class.
//Single Responsibility: The factory class knows about every database driver in existence.
//Limited Scope: Usually just returns one object (Connection). It doesn't help you figure out which Command object matches that connection.