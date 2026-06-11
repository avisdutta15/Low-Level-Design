//Third Party Libraries
//The Scenario: Two Incompatible SDKs
//You have two database libraries. You cannot change their code. notice they have different method names.

namespace NeedOfAdapterDatabaseExample
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

    public class ReportService
    {
        private MySqlSdk _mySqlSdk;
        private SqliteSdk _sqliteSdk;
        private string _dbType;

        public ReportService(string dbType)
        {
            _dbType = dbType;
            if (dbType == "mysql") _mySqlSdk = new MySqlSdk();
            else if (dbType == "sqlite") _sqliteSdk = new SqliteSdk();
        }

        public void GenerateReport()
        {
            if (_dbType == "mysql")
            {
                _mySqlSdk.OpenConnection("admin", "password");
                _mySqlSdk.RunQuery("SELECT * FROM reports");
            }
            else if (_dbType == "sqlite")
            {
                _sqliteSdk.Login("data.db");
                _sqliteSdk.ExecuteCommand("SELECT * FROM reports");
            }
            Console.WriteLine("Report Generated");
        }
    }

    public class Program
    {
        public static void Main(string[] args)
        {
            ReportService reportSerivce = new("mysql");
            reportSerivce.GenerateReport();
        }
    }
}

/*
 *  Why this is bad:-
 *      Polluted Logic: The service contains unrelated "plumbing" logic (checking types, handling specific parameters).
 *      Tight Coupling: The service depends directly on MySqlSdk AND SqliteSdk.
 *      Violation of OCP: To add Oracle support, you must modify this class and risk breaking the reporting logic.
 */