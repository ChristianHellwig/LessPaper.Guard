using LessPaper.Guard.Database.MongoDb.Models.Dtos;
using MongoDB.Driver;

namespace LessPaper.Guard.Database.MongoDb.Interfaces
{
    public interface IDatabaseManager
    {
        /// <summary>
        /// Mongo client
        /// </summary>
        IMongoClient Client { get; }

        /// <summary>
        /// Directories
        /// </summary>
        IMongoCollection<DirectoryDto> DirectoryCollection { get; }

        /// <summary>
        /// Users
        /// </summary>
        IMongoCollection<UserDto> UserCollection { get; }

        /// <summary>
        /// Files 
        /// </summary>
        IMongoCollection<FileDto> FilesCollection { get; }

        /// <summary>
        /// File revisions
        /// </summary>
        IMongoCollection<FileRevisionDto> FileRevisionCollection { get; }
    }
}
