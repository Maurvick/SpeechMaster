using System.IO;
using Microsoft.Data.Sqlite;

namespace ExperimentASR.Services
{
    public class DatabaseService
    {
        private readonly string _connectionString = "Data Source=MyNewDatabase.db";

        public async static void InitializeDatabase()
        {
            // Fix: Remove ApplicationData usage, use local file path directly
            string dbFileName = "sqliteSample.db";
            string dbpath = Path.Combine(Directory.GetCurrentDirectory(), dbFileName);

            if (!File.Exists(dbpath))
            {
                using (File.Create(dbpath)) { }
            }

            using (var db = new SqliteConnection($"Filename={dbpath}"))
            {
                db.Open();

                string tableCommand = "CREATE TABLE IF NOT " +
                    "EXISTS MyTable (Primary_Key INTEGER PRIMARY KEY, " +
                    "Text_Entry NVARCHAR(2048) NULL)";

                var createTable = new SqliteCommand(tableCommand, db);

                createTable.ExecuteReader();
            }
        }
    }
}
