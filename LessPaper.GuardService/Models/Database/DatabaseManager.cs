using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using LessPaper.GuardService.Models.Database.Dtos;
using LessPaper.Shared.Enums;
using LessPaper.Shared.Interfaces.Database.Manager;
using Microsoft.AspNetCore.Routing.Template;
using MongoDB.Bson;
using MongoDB.Driver;
using MongoDB.Driver.Core.Events;

namespace LessPaper.GuardService.Models.Database
{
    public class DatabaseManager : IDbManager
    {
        public DatabaseManager()
        {
            var cs = "mongodb://user1:masterkey@127.0.0.1:27017";

            var mongoClientSettings = MongoClientSettings.FromUrl(new MongoUrl(cs));
            mongoClientSettings.ClusterConfigurator = cb => {
                cb.Subscribe<CommandStartedEvent>(e => {
                    Trace.WriteLine($"{e.CommandName} - {e.Command.ToJson()}");
                });
            };
            var dbClient = new MongoClient(mongoClientSettings);


            var db = dbClient.GetDatabase("lesspaper");

            var userCollection = db.GetCollection<MinimalUserInformationDto>("user");
            var directoryCollection = db.GetCollection<DirectoryDto>("directories");
          

            DbFileManager = new DbFileManager(dbClient, directoryCollection, userCollection);
            DbDirectoryManager = new DbDirectoryManager(dbClient, directoryCollection);
            DbUserManager = new DbUserManager(dbClient, userCollection, directoryCollection);
            DbSearchManager = new DbSearchManager(db);
        }

        /// <inheritdoc />
        public IDbFileManager DbFileManager { get; }

        /// <inheritdoc />
        public IDbDirectoryManager DbDirectoryManager { get; }

        /// <inheritdoc />
        public IDbUserManager DbUserManager { get; }

        /// <inheritdoc />
        public IDbSearchManager DbSearchManager { get; }
    }
}
