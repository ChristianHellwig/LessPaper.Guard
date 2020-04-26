using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using LessPaper.GuardService.Models.Database.Dtos;
using LessPaper.GuardService.Models.Database.Implement;
using LessPaper.Shared.Enums;
using LessPaper.Shared.Helper;
using LessPaper.Shared.Interfaces.Database.Manager;
using LessPaper.Shared.Interfaces.General;
using LessPaper.Shared.Interfaces.GuardApi.Response;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Driver;
using MongoDB.Driver.Linq;

namespace LessPaper.GuardService.Models.Database
{
    public class DbDirectoryManager : IDbDirectoryManager
    {
        private readonly IMongoClient client;
        private readonly IMongoCollection<DirectoryDto> directoryCollection;
        
        public DbDirectoryManager(IMongoClient client, IMongoCollection<DirectoryDto> directoryCollection)
        {
            this.client = client;
            this.directoryCollection = directoryCollection;
        }

        /// <inheritdoc />
        public async Task<bool> DeleteDirectory(string requestingUserId, string directoryId)
        {
            if (IdGenerator.TypeFromId(requestingUserId, out var requestingUserIdType) && requestingUserIdType != IdType.User ||
                IdGenerator.TypeFromId(directoryId, out var directoryIdType) && directoryIdType != IdType.Directory)
            {
                return false;
            }

            var deleteResult = await directoryCollection.DeleteOneAsync(x =>
                x.Id == directoryId &&
                x.IsRootDirectory == false &&
                x.Permissions.Any(y =>
                    y.Permission.HasFlag(Permission.Write) &&
                    y.User == new MongoDBRef("user", requestingUserId)
                ));

            return deleteResult.DeletedCount == 1;
        }

        /// <inheritdoc />
        public async Task<bool> InsertDirectory(string requestingUserId, string parentDirectoryId, string directoryName, string newDirectoryId)
        {
            if (IdGenerator.TypeFromId(requestingUserId, out var requestingUserIdType) && requestingUserIdType != IdType.User ||
                IdGenerator.TypeFromId(parentDirectoryId, out var parentDirectoryIdType) && parentDirectoryIdType != IdType.Directory ||
                IdGenerator.TypeFromId(newDirectoryId, out var newDirectoryIdType) && newDirectoryIdType != IdType.Directory)
            {
                return false;
            }


            using var session = await client.StartSessionAsync();
            session.StartTransaction();

            try
            {
                var userIdRef = await directoryCollection
                    .Find(x => x.Id == parentDirectoryId)
                    .Project(x => x.Owner)
                    .FirstAsync();

                var newDirectory = new DirectoryDto
                {
                    Id = newDirectoryId,
                    IsRootDirectory = false,
                    Owner = userIdRef,
                    ObjectName = directoryName,
                    Directories = new List<MongoDBRef>(),
                    Files = new List<FileDto>(),
                    Permissions = new[]
                    {
                        new DirectoryPermissionDto
                        {
                            User = userIdRef,
                            Permission = Permission.Read | Permission.Write | Permission.ReadPermissions | Permission.WritePermissions
                        }
                    },
                };

                var insertDirectoryTask = directoryCollection.InsertOneAsync(newDirectory);

                var update = Builders<DirectoryDto>.Update
                    .Push(e => e.Directories, new MongoDBRef("directories", newDirectoryId));

                var updateResultTask = directoryCollection.UpdateOneAsync(x => x.Id == parentDirectoryId, update);

                await Task.WhenAll(insertDirectoryTask, updateResultTask);
                if (updateResultTask.Result.ModifiedCount != 1)
                {
                    await session.AbortTransactionAsync();
                    return false;
                }

                await session.CommitTransactionAsync();
            }
            catch (Exception e)
            {
                Console.WriteLine("Error writing to MongoDB: " + e.Message);
                await session.AbortTransactionAsync();
                return false;
            }

            return true;
        }

        /// <inheritdoc />
        public async Task<IPermissionResponse[]> GetDirectoryPermissions(string requestingUserId, string userId, string[] objectIds)
        {
            
            var directoryPermisssions = await directoryCollection
                .AsQueryable()
                .Where(x =>
                    objectIds.Contains(x.Id) &&
                    x.Permissions.Any(y =>
                        y.Permission.HasFlag(Permission.ReadPermissions) &&
                        y.User == new MongoDBRef("user", requestingUserId)
                    ))
                .Select(x => new
                {
                        Id = x.Id,
                        Permissions = x.Permissions //.Where(y => y.User == new MongoDBRef("user", requestingUserId))
                })
                .ToListAsync();





            return null;
        }

        /// <inheritdoc />
        public async Task<IDirectoryMetadata> GetDirectoryMetadata(string requestingUserId, string directoryId, uint? revisionNumber)
        {
            try
            {
                var directory = await directoryCollection.Find(x =>
                    x.Id == directoryId &&
                    x.Permissions.Any(y =>
                        y.Permission.HasFlag(Permission.Read) &&
                        y.User == new MongoDBRef("user", requestingUserId)
                    )).FirstOrDefaultAsync();

                if (directory == null)
                    return null;

                var childDirectoryIds = directory.Directories.Select(x => x.Id.AsString).ToArray();
                var directoryDtoChilds = await directoryCollection
                    .AsQueryable()
                    .Where(x => childDirectoryIds.Contains(x.Id))
                    .Select(x => new MinimalDirectoryMetadataDto()
                    {
                        NumberOfChilds = (uint)(x.Files.Count + x.Directories.Count),
                        ObjectId = x.Id,
                        ObjectName = x.ObjectName,
                        Permissions = x.Permissions
                    }).ToListAsync();
                
                
                var childDirectories = directoryDtoChilds
                    .Select(x => new MinimalDirectoryMetadata(x))
                    .Cast<IMinimalDirectoryMetadata>()
                    .ToArray();

                var childFiles = directory.Files
                    .Select(x => new File(x))
                    .Cast<IFileMetadata>()
                    .ToArray();
                    
                return new Directory(directory, childFiles, childDirectories);
            }
            catch (Exception e)
            {
                Console.WriteLine("Error writing to MongoDB: " + e.Message);
             
                return null;
            }
        }
    }
}
