using SeparateBuilder;

namespace SeparateBuilder
{
    // The complex object we want to build
    public class DbConnectionString
    {
        public string Server { get; }
        public int Port { get; }
        public string Database { get; }
        public string Username { get; }
        public string Password { get; }
        public int ConnectionTimeout { get; } = 30;                //Optional
        public int CommandTimeout { get; } = 30;                   //Optional
        public int MaxPoolSize { get; } = 100;                     //Optional
        public int MinPoolSize { get; } = 0;                       //Optional
        public string ApplicationName { get; } = string.Empty;     //Optional
        public bool UseSSL { get; } = true;                        //Optional
        public bool TrustServerCertificate { get; } = false;       //Optional

        // Internal constructor - only builder can create instances
        internal DbConnectionString(
            string server,
            int port,
            string database,
            string username,
            string password,
            int connectionTimeout,
            int commandTimeout,
            bool useSSL,
            bool trustServerCertificate,
            string applicationName,
            int maxPoolSize,
            int minPoolSize)
        {
            Server = server;
            Port = port;
            Database = database;
            Username = username;
            Password = password;
            ConnectionTimeout = connectionTimeout;
            CommandTimeout = commandTimeout;
            UseSSL = useSSL;
            TrustServerCertificate = trustServerCertificate;
            ApplicationName = applicationName;
            MaxPoolSize = maxPoolSize;
            MinPoolSize = minPoolSize;
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

    // Builder class - provides fluent interface for construction
    public class DbConnectionStringBuilder
    {
        // Required parameters
        private string _server;
        private string _database;
        private string _username;
        private string _password;

        // Optional parameters with defaults
        private int _port = 1433;
        private int _connectionTimeout = 30;
        private int _commandTimeout = 30;
        private bool _useSSL = true;
        private bool _trustServerCertificate = false;
        private string _applicationName = "MyApp";
        private int _maxPoolSize = 100;
        private int _minPoolSize = 0;

        // Fluent methods for required parameters

        public DbConnectionStringBuilder WithServer(string server)
        {
            _server = server ?? throw new ArgumentNullException(nameof(server));
            return this;
        }
        public DbConnectionStringBuilder WithDatabase(string database)
        {
            _database = database ?? throw new ArgumentNullException(nameof(database));
            return this;
        }

        public DbConnectionStringBuilder WithCredentials(string username, string password)
        {
            _username = username ?? throw new ArgumentNullException(nameof(username));
            _password = password ?? throw new ArgumentNullException(nameof(password));
            return this;
        }



        // Fluent methods for optional parameters
        public DbConnectionStringBuilder WithPort(int port)
        {
            if (port <= 0) throw new ArgumentException("Port must be positive", nameof(port));
            _port = port;
            return this;
        }
        public DbConnectionStringBuilder WithConnectionTimeout(int seconds)
        {
            if (seconds < 0) throw new ArgumentException("Timeout cannot be negative", nameof(seconds));
            _connectionTimeout = seconds;
            return this;
        }

        public DbConnectionStringBuilder WithCommandTimeout(int seconds)
        {
            if (seconds < 0) throw new ArgumentException("Timeout cannot be negative", nameof(seconds));
            _commandTimeout = seconds;
            return this;
        }

        public DbConnectionStringBuilder WithSSL(bool useSSL)
        {
            _useSSL = useSSL;
            return this;
        }

        public DbConnectionStringBuilder WithTrustServerCertificate(bool trust)
        {
            _trustServerCertificate = trust;
            return this;
        }

        public DbConnectionStringBuilder WithApplicationName(string appName)
        {
            _applicationName = appName ?? throw new ArgumentNullException(nameof(appName));
            return this;
        }

        public DbConnectionStringBuilder WithPoolSize(int min, int max)
        {
            if (min < 0) throw new ArgumentException("Min pool size cannot be negative", nameof(min));
            if (max < min) throw new ArgumentException("Max pool size must be >= min", nameof(max));
            _minPoolSize = min;
            _maxPoolSize = max;
            return this;
        }

        public DbConnectionString Build()
        {
            // Validate required parameters
            if (string.IsNullOrWhiteSpace(_server))
                throw new InvalidOperationException("Server is required");
            if (string.IsNullOrWhiteSpace(_database))
                throw new InvalidOperationException("Database is required");
            if (string.IsNullOrWhiteSpace(_username))
                throw new InvalidOperationException("Username is required");
            if (string.IsNullOrWhiteSpace(_password))
                throw new InvalidOperationException("Password is required");

            return new DbConnectionString(
                _server,
                _port,
                _database,
                _username,
                _password,
                _connectionTimeout,
                _commandTimeout,
                _useSSL,
                _trustServerCertificate,
                _applicationName,
                _maxPoolSize,
                _minPoolSize);
        }
    }
}

namespace SeparateBuilderDemo
{
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

    public class Program
    {
        public static void Main(string[] args)
        {
            //Readable.
            //Only Set what we need.
            //Order doesnot matter to the client
            //Immutability
            var connectionString = new DbConnectionStringBuilder()
                .WithServer("localhost")
                .WithDatabase("mydb")
                .WithCredentials("admin", "pass123")
                .WithConnectionTimeout(60)  // Only set what you need
                .Build();

            // Forgot database and credentials!
            var invalid = new DbConnectionStringBuilder()
                    .WithServer("localhost")
                    // Forgot database and credentials! No worries, validation at build time
                    .Build();

            //Immutable
            var immutableConn = new DbConnectionStringBuilder()
                .WithServer("localhost")
                .WithDatabase("mydb")
                .WithCredentials("admin", "pass123")
                .WithConnectionTimeout(60)  // Only set what you need
                .Build();

            //Below is not possible as there are no setters!
            //immutableConn.Server = "hacked";

            SqlConnection sqlConnection = new SqlConnection(connectionString);
            sqlConnection.Open();
        }
    }    
}

//BluePrint:
//Create the Product class whose object we want to build.
//           class - public
//           properties - public with no setters.
//           only one constructor - internal (coz can only be called by its Builder who is in the same namespace)
//
//Create the Builder class.
//           class - public
//           fields - private
//           fluent methods returning Builder
//           one Build() method returning the concrete Product.
//           The Build() method has validations for the required fields.