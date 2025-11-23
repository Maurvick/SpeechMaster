using Microsoft.Data.Sqlite;

namespace ExperimentASR.Services
{
    public class DatabaseService
    {
        private readonly string _connectionString = "Data Source=MyNewDatabase.db";

        private void ConnectDB()
        {
            try
            {
                using (var connection = new SqliteConnection(_connectionString))
                {
                    connection.Open();
                    var command = connection.CreateCommand();
                    command.CommandText = "CREATE TABLE IF NOT EXISTS Settings (Key TEXT, Value TEXT)";
                    command.ExecuteNonQuery();
                }
            }
            catch (SqliteException ex)
            {
                throw new Exception("Database connection failed: " + ex.Message);
            }
        }

        
    }
}
