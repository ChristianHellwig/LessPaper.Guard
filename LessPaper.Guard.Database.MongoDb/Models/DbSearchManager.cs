using System;
using System.Threading.Tasks;
using LessPaper.Guard.Database.MongoDb.Interfaces;
using LessPaper.Guard.Database.MongoDb.Models.Dtos;
using LessPaper.Shared.Interfaces.Database.Manager;
using LessPaper.Shared.Interfaces.General;
using MongoDB.Driver;

namespace LessPaper.Guard.Database.MongoDb.Models
{
    public class DbSearchManager : IDbSearchManager
    {
        private readonly IMongoClient client;
        private readonly IMongoCollection<DirectoryDto> directoryCollection;
        private readonly IMongoCollection<UserDto> userCollection;
        private readonly IMongoCollection<FileDto> filesCollection;
        private readonly IMongoCollection<FileRevisionDto> fileRevisionCollection;


        public DbSearchManager(IDatabaseManager dbManager)
        {
            client = dbManager.Client;
            directoryCollection = dbManager.DirectoryCollection;
            userCollection = dbManager.UserCollection;
            filesCollection = dbManager.FilesCollection;
            fileRevisionCollection = dbManager.FileRevisionCollection;
        }

        /// <inheritdoc />
        public async Task<ISearchResult> Search(string requestingUserId, string directoryId, string searchQuery, uint count, uint page)
        {
            throw new NotImplementedException();
        }
    }
}
