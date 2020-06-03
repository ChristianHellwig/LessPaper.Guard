using LessPaper.Guard.Database.MongoDb.Models;
using LessPaper.Shared.Interfaces.Database.Manager;

namespace LessPaper.Guard.Database.MongoDb.IntegrationTest
{
    public class MongoTestBase
    {
        protected readonly IDbUserManager UserManager;
        protected readonly IDbDirectoryManager DirectoryManager;
        protected readonly IDbFileManager FileManager;

        public MongoTestBase()
        {
            var settings = new DatabaseSettings("mongodb://192.168.0.227:28017?retryWrites=false");
            var databaseManager = new DatabaseManager(settings);

            UserManager = new DbUserManager(databaseManager);
            DirectoryManager = new DbDirectoryManager(databaseManager);
            FileManager = new DbFileManager(databaseManager);
        }

  

    }
}
