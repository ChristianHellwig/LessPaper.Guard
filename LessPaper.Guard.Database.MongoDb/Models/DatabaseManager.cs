using System.Diagnostics;
using LessPaper.Guard.Database.MongoDb.Interfaces;
using LessPaper.Guard.Database.MongoDb.Models.Dtos;
using LessPaper.Shared.Interfaces.Database;
using MongoDB.Bson;
using MongoDB.Driver;
using MongoDB.Driver.Core.Events;

namespace LessPaper.Guard.Database.MongoDb.Models
{
    public class DatabaseManager : IDatabaseManager
    {
        public DatabaseManager(IDatabaseSettings settings)
        {
            var mongoClientSettings = MongoClientSettings.FromUrl(new MongoUrl(settings.ConnectionString));
            mongoClientSettings.RetryWrites = false;

#if DEBUG
            // Print translated mongo commands
            mongoClientSettings.ClusterConfigurator = cb =>
            {
                cb.Subscribe<CommandStartedEvent>(e =>
                {
                    Trace.WriteLine($"{e.CommandName} - {e.Command.ToJson()}");
                });
            };
#endif

            var dbClient = new MongoClient(mongoClientSettings);
            var tables = new MongoTables();
            var db = dbClient.GetDatabase(tables.DatabaseName);

            Client = dbClient;
            UserCollection = db.GetCollection<UserDto>(tables.UserTable);
            DirectoryCollection = db.GetCollection<DirectoryDto>(tables.DirectoryTable);
            FilesCollection = db.GetCollection<FileDto>(tables.FilesTable);
            FileRevisionCollection = db.GetCollection<FileRevisionDto>(tables.RevisionTable);

            // Build database collections & indices
            CreateCollections(tables, db);
            CreateIndex();
        }

        /// <summary>
        /// Create database indices
        /// </summary>
        private void CreateIndex()
        {
            // Create index 

            #region  - UserId -

            var uniqueEmail = new CreateIndexModel<UserDto>(
                Builders<UserDto>.IndexKeys.Ascending(x => x.Email),
                new CreateIndexOptions { Unique = true });

            UserCollection.Indexes.CreateOne(uniqueEmail);

            #endregion

            #region - Directory -

            var uniqueDirectoryName = new CreateIndexModel<DirectoryDto>(
                Builders<DirectoryDto>.IndexKeys.Combine(
                    Builders<DirectoryDto>.IndexKeys.Ascending(x => x.OwnerId),
                    Builders<DirectoryDto>.IndexKeys.Ascending(x => x.ObjectName),
                    Builders<DirectoryDto>.IndexKeys.Ascending(x => x.ParentDirectoryId)
                ),
                new CreateIndexOptions { Unique = true });

            DirectoryCollection.Indexes.CreateOne(uniqueDirectoryName);

            #endregion

            #region - FileIds -

            var uniqueFileNames = new CreateIndexModel<FileDto>(
                Builders<FileDto>.IndexKeys.Combine(
                    Builders<FileDto>.IndexKeys.Ascending(x => x.OwnerId),
                    Builders<FileDto>.IndexKeys.Ascending(x => x.ObjectName),
                    Builders<FileDto>.IndexKeys.Ascending(x => x.ParentDirectoryId)
                ),
                new CreateIndexOptions { Unique = true });

            FilesCollection.Indexes.CreateOne(uniqueFileNames);

            #endregion

            #region - Revision -

            var revisionIndex = new CreateIndexModel<FileRevisionDto>(
                Builders<FileRevisionDto>.IndexKeys.Combine(
                    Builders<FileRevisionDto>.IndexKeys.Ascending(x => x.File)
                ),
                new CreateIndexOptions { Unique = false });

            FileRevisionCollection.Indexes.CreateOne(revisionIndex);

            #endregion
        }

        /// <summary>
        /// Create database tables if not exists
        /// </summary>
        /// <param name="tables"></param>
        /// <param name="db"></param>
        private void CreateCollections(IMongoTables tables, IMongoDatabase db)
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


        /// <summary>
        /// Check if a collection exists
        /// </summary>
        /// <param name="database">MongoDatabase</param>
        /// <param name="collectionName">Collection</param>
        /// <returns></returns>
        public bool CollectionExists(IMongoDatabase database, string collectionName)
        {
            var filter = new BsonDocument("name", collectionName);
            var options = new ListCollectionNamesOptions { Filter = filter };

            return database.ListCollectionNames(options).Any();
        }

        /// <inheritdoc />
        public IMongoClient Client { get; }

        /// <inheritdoc />
        public IMongoCollection<DirectoryDto> DirectoryCollection { get; }

        /// <inheritdoc />
        public IMongoCollection<UserDto> UserCollection { get; }

        /// <inheritdoc />
        public IMongoCollection<FileDto> FilesCollection { get; }

        /// <inheritdoc />
        public IMongoCollection<FileRevisionDto> FileRevisionCollection { get; }
    }
}
