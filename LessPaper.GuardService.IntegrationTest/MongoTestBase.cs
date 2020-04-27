using System;
using System.Collections.Generic;
using System.Text;
using LessPaper.GuardService.Models.Database;
using LessPaper.Shared.Enums;
using LessPaper.Shared.Helper;
using LessPaper.Shared.Interfaces.Database.Manager;
using MongoDB.Driver;

namespace LessPaper.GuardService.IntegrationTest
{
    public class MongoTestBase
    {
        protected readonly string User1Id = IdGenerator.NewId(IdType.User);
        protected readonly string User1RootDirId = IdGenerator.NewId(IdType.Directory);
        protected readonly string User1HashedPassword = "HashedPassword";
        protected readonly string User1Salt = "Salt";
        protected readonly string User1Email = "User1@test.com";

        protected readonly string User2Id = IdGenerator.NewId(IdType.User);
        protected readonly string User2RootDirId = IdGenerator.NewId(IdType.Directory);
        protected readonly string User2HashedPassword = "HashedPassword";
        protected readonly string User2Salt = "Salt";
        protected readonly string User2Email = "User2@test.com";


        protected readonly IDbUserManager UserManager;
        protected readonly IDbDirectoryManager DirectoryManager;

        public MongoTestBase()
        {
            var cs = "mongodb://user1:masterkey@127.0.0.1:27017";
            var mongoClientSettings = MongoClientSettings.FromUrl(new MongoUrl(cs));
            var dbClient = new MongoClient(mongoClientSettings);
            var db = dbClient.GetDatabase("lesspaper");
            db.DropCollection("user");

            var databaseManager = new DatabaseManager();
            UserManager = databaseManager.DbUserManager;
            DirectoryManager = databaseManager.DbDirectoryManager;
        }

    }
}
