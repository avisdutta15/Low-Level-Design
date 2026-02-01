DbConnectionString connectionString = new DbConnectionString("localhost", "test-db", "admin", "password");
SqlConnection sqlConnection = new SqlConnection(connectionString);
sqlConnection.Open();

// Problem: Which constructor do I use? What order are parameters?
// Problem: Can't skip optional parameters in the middle
// Problem: Hard to read: new DBConnectionStringProblematic("server", 1433, "db", "user", "pass", 30, 60)
DbConnectionString connectionString1 = new DbConnectionString("localhost", 80 ,"test-db", "admin", "password");
SqlConnection sqlConnection1 = new SqlConnection(connectionString1);
sqlConnection1.Open();

public class SqlConnection
{
    private DbConnectionString _connectionString;
    public SqlConnection(DbConnectionString connectionString)
    {
        _connectionString = connectionString;
    }
    public void Open()
    {
        // Cannot Accidentally modifies the connection
        //_connectionString.Server = "malicious-server";  //This is because the property is private.
        Console.WriteLine($"Opened connection...{_connectionString.ToString()}");
    }
}


public class DbConnectionString
{
    private string Server { get; set; }
    private int Port { get; set; }
    private string Database { get; set; }
    private string Username { get; set; }
    private string Password { get; set; }
    private int ConnectionTimeout { get; set; } = 30;                //Optional
    private int CommandTimeout { get; set; } = 30;                   //Optional
    private int MaxPoolSize { get; set; } = 100;                     //Optional
    private int MinPoolSize { get; set; } = 0;                       //Optional
    private string ApplicationName { get; set; } = string.Empty;     //Optional
    private bool UseSSL { get; set; } = true;                        //Optional
    private bool TrustServerCertificate { get; set; } = false;       //Optional

    // Problem 1: Telescoping constructor pattern
    public DbConnectionString(string server, string database, string username, string password)
    {
        Server = server;
        Database = database;
        Username = username;
        Password = password;
        Port = 1433;
        ConnectionTimeout = 30;
        CommandTimeout = 30;
        UseSSL = true;
        TrustServerCertificate = false;
        ApplicationName = "MyApp";
        MaxPoolSize = 100;
        MinPoolSize = 0;
    }

    // Need multiple constructors for different combinations
    public DbConnectionString(string server, int port, string database, string username, string password)
        : this(server, database, username, password)
    {
        Port = port;
    }

    // This gets out of hand quickly with 12 parameters!
    public DbConnectionString(
        string server, int port, string database, string username, string password,
        int connectionTimeout, int commandTimeout)
        : this(server, port, database, username, password)
    {
        ConnectionTimeout = connectionTimeout;
        CommandTimeout = commandTimeout;
    }

    public override string ToString()
    {
        return $"Server={Server};Port={Port};Database={Database};" +
               $"User Id={Username};Password={Password};" +
               $"Connection Timeout={ConnectionTimeout};Command Timeout={CommandTimeout};" +
               $"Encrypt={UseSSL};TrustServerCertificate={TrustServerCertificate};" +
               $"Application Name={ApplicationName};" +
               $"Max Pool Size={MaxPoolSize};Min Pool Size={MinPoolSize}";
    }
}