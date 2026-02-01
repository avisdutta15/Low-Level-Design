DbConnectionString connectionString = new DbConnectionString();
connectionString.Server = "localhost";
connectionString.Port = "3232";
connectionString.Database = "test-db";
connectionString.Username = "admin";
connectionString.Password = "password";
string conStr = connectionString.ToString();

//Problem 1: Mutability
//we want the connection string to be immutable.
//since we have used properties, we can set the connection string anywhere
SqlConnection sqlConnection = new SqlConnection(connectionString);
Console.WriteLine($"Original Server: {connectionString.Server}");
sqlConnection.Open();
Console.WriteLine($"After modifying original: {connectionString.Server}");


// Problem 2: Temporal Coupling
// Must set properties in correct order
connectionString.UseSSL = true;
connectionString.TrustServerCertificate = false;  // Only valid when UseSSL is true

//Because of these issues, we can use the constructor instead of getters and setters.
//Constructors would ensure that the values are set only once.
//But they have their own challanges.


public class SqlConnection
{
    private DbConnectionString _connectionString;
    public SqlConnection(DbConnectionString connectionString)
    {
        _connectionString = connectionString;
    }
    public void Open()
    {
        // Accidentally modifies the connection
        _connectionString.Server = "malicious-server";  // Oops! This affects the original
        Console.WriteLine("Opened connection...");
    }
}


public class DbConnectionString
{
    public string Server { get; set; }
    public string Port { get; set; }
    public string Database { get; set; }
    public string Username { get; set; }
    public string Password { get; set; }
    public int ConnectionTimeout { get; set; } = 30;                //Optional
    public int CommandTimeout { get; set; } = 30;                   //Optional
    public int MaxPoolSize { get; set; } = 100;                     //Optional
    public int MinPoolSize { get; set; } = 0;                       //Optional
    public string ApplicationName { get; set; } = string.Empty;     //Optional
    public bool UseSSL { get; set; } = true;                        //Optional
    public bool TrustServerCertificate { get; set; } = false;       //Optional

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