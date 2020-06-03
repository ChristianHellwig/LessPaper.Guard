using LessPaper.Shared.Interfaces.Database;

namespace LessPaper.Guard.Database.MongoDb.IntegrationTest
{
    public class DatabaseSettings : IDatabaseSettings
    {
        public DatabaseSettings(string connectionString)
        {
            ConnectionString = connectionString;
        }

        /// <inheritdoc />
        public string ConnectionString { get; }
    }
}
