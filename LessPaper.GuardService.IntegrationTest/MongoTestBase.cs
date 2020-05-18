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



        protected readonly IDbUserManager UserManager;
        protected readonly IDbDirectoryManager DirectoryManager;
        protected readonly IDbFileManager FileManager;

        public MongoTestBase()
        {
            //var cs = "mongodb://user1:masterkey@127.0.0.1:27017";
            var cs = "mongodb://192.168.0.227:28017?retryWrites=false";
            var mongoClientSettings = MongoClientSettings.FromUrl(new MongoUrl(cs));
            var dbClient = new MongoClient(mongoClientSettings);
            var db = dbClient.GetDatabase("lesspaper");



            var databaseManager = new DatabaseManager();
            UserManager = databaseManager.DbUserManager;
            DirectoryManager = databaseManager.DbDirectoryManager;
            FileManager = databaseManager.DbFileManager;
        }

  

    }
}
