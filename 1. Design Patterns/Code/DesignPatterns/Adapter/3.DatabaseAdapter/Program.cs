namespace DatabaseAdapterExample
{
    // SDK 1: MySQL
    public class MySqlSdk
    {
        public void OpenConnection(string u, string p) { /* ... */ }
        public void RunQuery(string sql) { /* ... */ }
    }

    // SDK 2: SQLite
    public class SqliteSdk
    {
        public void Login(string dbFile) { /* ... */ }
        public void ExecuteCommand(string cmd) { /* ... */ }
    }

    // 3. The Common Interface
    public interface IReportDatabaseAdapter
    {
        void Connect();
        void Query(string sql);
    }

    // 4. The Adapters (Hiding the ugly details)
    public class MySqlSdkAdapter : IReportDatabaseAdapter
    {
        private MySqlSdk _mySqlSdk = new MySqlSdk();
        public void Connect() => _mySqlSdk.OpenConnection("admin", "password");
        public void Query(string sql) => _mySqlSdk.RunQuery(sql);
    }

    public class SqliteSdkAdapter : IReportDatabaseAdapter
    {
        private SqliteSdk _mySqlSdk = new SqliteSdk();
        public void Connect() => _mySqlSdk.Login("data.db");
        public void Query(string sql) => _mySqlSdk.ExecuteCommand(sql);
    }

    // 5. The Clean Service
    public class ReportService
    {
        private readonly IReportDatabaseAdapter _adapter;

        // Dependency Injection
        public ReportService(IReportDatabaseAdapter adapter)
        {
            _adapter = adapter;
        }

        public void GenerateReport()
        {
            // The service is now "Universal"
            _adapter.Connect();
            _adapter.Query("SELECT * FROM reports");

            Console.WriteLine("Report Generated");
        }
    }

    public class Program
    {
        public static void Main(string[] args)
        {
            ReportService reportSerivce = new(new MySqlSdkAdapter());
            reportSerivce.GenerateReport();
        }
    }
}

/*
 *   We introduce an interface IDatabase and two Adapters.
 *   Now look at the ReportService.
 *   Why this is good:
 *       Clean Logic: No if-else. No method renaming logic.
 *       Decoupled: The service doesn't know MySQL or SQLite exist. It only knows IDatabase.
 *       Extensible: To add Oracle, you create OracleAdapter. You do not touch ReportService.
 */