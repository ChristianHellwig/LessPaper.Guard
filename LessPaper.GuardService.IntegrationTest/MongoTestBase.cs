using System;
using System.Collections.Generic;
using System.Text;
using LessPaper.GuardService.Models.Database;
using LessPaper.Shared.Enums;
using LessPaper.Shared.Helper;
using LessPaper.Shared.Interfaces.Database.Manager;
using MongoDB.Bson;
using MongoDB.Driver;

namespace LessPaper.GuardService.IntegrationTest
{
    public class MongoTestBase
    {
        protected string User1Id = IdGenerator.NewId(IdType.User);
        protected string User1RootDirId = IdGenerator.NewId(IdType.Directory);
        protected string User1HashedPassword = "HashedPassword";
        protected string User1Salt = "Salt";
        protected string User1Email = "1@t.de";
        protected CryptoHelper.RsaKeyPair User1Keys = CryptoHelper.GenerateRsaKeyPair();

        protected string User2Id = IdGenerator.NewId(IdType.User);
        protected string User2RootDirId = IdGenerator.NewId(IdType.Directory);
        protected string User2HashedPassword = "HashedPassword";
        protected string User2Salt = "Salt";
        protected string User2Email = "2@t.de";
        protected CryptoHelper.RsaKeyPair User2Keys = CryptoHelper.GenerateRsaKeyPair();


        protected readonly IDbUserManager UserManager;
        protected readonly IDbDirectoryManager DirectoryManager;
        protected readonly IDbFileManager FileManager;

        public MongoTestBase()
        {
            User1Email = User1Id + "@test.com";
            User2Email = User2Id + "@test.com";

            //var cs = "mongodb://user1:masterkey@127.0.0.1:27017";
            var cs = "mongodb://192.168.0.227:28017?retryWrites=false";
            var mongoClientSettings = MongoClientSettings.FromUrl(new MongoUrl(cs));
            var dbClient = new MongoClient(mongoClientSettings);
            var db = dbClient.GetDatabase("lesspaper");


            if (!CollectionExists(db, "user"))
                db.CreateCollection("user");

            if (!CollectionExists(db, "directories"))
                db.CreateCollection("directories");

            var databaseManager = new DatabaseManager();
            UserManager = databaseManager.DbUserManager;
            DirectoryManager = databaseManager.DbDirectoryManager;
            FileManager = databaseManager.DbFileManager;
        }

        public bool CollectionExists(IMongoDatabase database, string collectionName)
        {
            var filter = new BsonDocument("name", collectionName);
            var options = new ListCollectionNamesOptions { Filter = filter };

            return database.ListCollectionNames(options).Any();
        }

    }
}
