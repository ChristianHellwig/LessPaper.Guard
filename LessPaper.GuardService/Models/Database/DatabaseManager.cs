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
            //var cs = "mongodb://user1:masterkey@127.0.0.1:27017?retryWrites=false";
            var cs = "mongodb://192.168.0.227:28017?retryWrites=false";


            var mongoClientSettings = MongoClientSettings.FromUrl(new MongoUrl(cs));
            mongoClientSettings.RetryWrites = false;
            //mongoClientSettings.ReadPreference = ReadPreference.PrimaryPreferred;

            mongoClientSettings.ClusterConfigurator = cb => {
                cb.Subscribe<CommandStartedEvent>(e => {
                    Trace.WriteLine($"{e.CommandName} - {e.Command.ToJson()}");
                });
            };
            var dbClient = new MongoClient(mongoClientSettings);


            var tables = new MongoTables();

            var db = dbClient.GetDatabase(tables.DatabaseName);
            EnsureTablesExist(tables, db);

            var userCollection = db.GetCollection<UserDto>(tables.UserTable);
            var directoryCollection = db.GetCollection<DirectoryDto>(tables.DirectoryTable);
            var filesCollection = db.GetCollection<FileDto>(tables.FilesTable);
            var fileRevisionCollection = db.GetCollection<FileRevisionDto>(tables.RevisionTable);

            
            // Create index 

            #region  - UserId -

            var uniqueEmail = new CreateIndexModel<UserDto>(
                Builders<UserDto>.IndexKeys.Ascending(x => x.Email),
                new CreateIndexOptions { Unique = true });
            
            userCollection.Indexes.CreateOne(uniqueEmail);

            #endregion

            #region - Directory -

            var uniqueDirectoryName = new CreateIndexModel<DirectoryDto>(
                Builders<DirectoryDto>.IndexKeys.Combine(
                    Builders<DirectoryDto>.IndexKeys.Ascending(x => x.OwnerId),
                    Builders<DirectoryDto>.IndexKeys.Ascending(x => x.ObjectName),
                    Builders<DirectoryDto>.IndexKeys.Ascending(x => x.ParentDirectoryId)
                ),
                new CreateIndexOptions { Unique = true });

            directoryCollection.Indexes.CreateOne(uniqueDirectoryName);

            #endregion

            #region - FileIds -

            var uniqueFileNames = new CreateIndexModel<FileDto>(
                Builders<FileDto>.IndexKeys.Combine(
                    Builders<FileDto>.IndexKeys.Ascending(x => x.ObjectName),
                    Builders<FileDto>.IndexKeys.Ascending(x => x.ParentDirectoryId)
                ),
                new CreateIndexOptions { Unique = true });

            filesCollection.Indexes.CreateOne(uniqueFileNames);

            #endregion


            #region - Revision -

            var testIdx = new CreateIndexModel<FileRevisionDto>(
                Builders<FileRevisionDto>.IndexKeys.Combine(
                    Builders<FileRevisionDto>.IndexKeys.Ascending(x => x.File)
                ),
                new CreateIndexOptions { Unique = false });

            fileRevisionCollection.Indexes.CreateOne(testIdx);

            #endregion



            DbFileManager = new DbFileManager(tables, dbClient, directoryCollection, userCollection, filesCollection, fileRevisionCollection);
            DbDirectoryManager = new DbDirectoryManager(tables, dbClient, directoryCollection, userCollection, filesCollection, fileRevisionCollection);
            DbUserManager = new DbUserManager(tables, dbClient, userCollection, directoryCollection, filesCollection, fileRevisionCollection);
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

        private void EnsureTablesExist(IMongoTables tables, IMongoDatabase db)
        {
            try
            {
                if (!CollectionExists(db, tables.UserTable))
                    db.CreateCollection(tables.UserTable);

                if (!CollectionExists(db, tables.DirectoryTable))
                    db.CreateCollection(tables.DirectoryTable);

                if (!CollectionExists(db, tables.FilesTable))
                    db.CreateCollection(tables.FilesTable);

                if (!CollectionExists(db, tables.RevisionTable))
                    db.CreateCollection(tables.RevisionTable);
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                throw;
            }
        }


        public bool CollectionExists(IMongoDatabase database, string collectionName)
        {
            var filter = new BsonDocument("name", collectionName);
            var options = new ListCollectionNamesOptions { Filter = filter };

            return database.ListCollectionNames(options).Any();
        }
    }
}
