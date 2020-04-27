using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using LessPaper.GuardService.Models.Database.Dtos;
using LessPaper.GuardService.Models.Database.Helper;
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
                    y.Permission.HasFlag(Permission.ReadWrite) &&
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
                var parentDirectory = await directoryCollection.AsQueryable()
                    .Where(x =>
                        x.Id == parentDirectoryId &&
                        x.Permissions.Any(y =>
                            y.Permission.HasFlag(Permission.ReadWrite) &&
                            y.User == new MongoDBRef("user", requestingUserId)
                        ))
                    .Select(x => new
                    {
                        Owner = x.Owner,
                        Permissions = x.Permissions
                    })
                    .FirstOrDefaultAsync();

                var newDirectory = new DirectoryDto
                {
                    Id = newDirectoryId,
                    IsRootDirectory = false,
                    Owner = parentDirectory.Owner,
                    ObjectName = directoryName,
                    Directories = new List<MongoDBRef>(),
                    Files = new List<FileDto>(),
                    Permissions = parentDirectory.Permissions
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
            var directoryPermissions = await directoryCollection
                .AsQueryable()
                .Where(x =>
                    objectIds.Contains(x.Id) &&
                    x.Permissions.Any(y =>
                        (
                            y.Permission.HasFlag(Permission.ReadPermissions) || 
                            y.Permission.HasFlag(Permission.Read)
                        ) &&
                        y.User == new MongoDBRef("user", requestingUserId)
                    ))
                .Select(x => new
                {
                        Id = x.Id,
                        Permissions = x.Permissions
                })
                .ToListAsync();


            var responseObj = new List<IPermissionResponse>(directoryPermissions.Count);
            if (requestingUserId == userId)
            {
                // Require at least a read flag
                responseObj.AddRange((from directoryPermission in directoryPermissions
                    let permissionEntry = directoryPermission.Permissions
                        .FilterHasPermission(requestingUserId)
                        .FirstOrDefault()
                    where permissionEntry != null
                    select new PermissionResponse(directoryPermission.Id, permissionEntry.Permission)).Cast<IPermissionResponse>());
            }
            else
            {
                // Require ReadPermissions flag
                responseObj.AddRange((from directoryPermission in directoryPermissions
                    let permissionEntry = directoryPermission.Permissions
                        .FilterHasPermission(requestingUserId)
                        .FirstOrDefault(x => x.User.Id.AsString == userId)
                    where permissionEntry != null
                    select new PermissionResponse(directoryPermission.Id, permissionEntry.Permission)).Cast<IPermissionResponse>());
            }


            return responseObj.ToArray();
        }

        /// <inheritdoc />
        public async Task<IDirectoryMetadata> GetDirectoryMetadata(string requestingUserId, string directoryId, uint? revisionNumber)
        {
            try
            {
                // Get Root directory
                var directory = await directoryCollection.Find(x =>
                    x.Id == directoryId &&
                    x.Permissions.Any(y =>
                        y.Permission.HasFlag(Permission.Read) &&
                        y.User == new MongoDBRef("user", requestingUserId)
                    )).FirstOrDefaultAsync();

                if (directory == null)
                    return null;

                // Get Child directories
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
                
                // Filter directory permissions
                foreach (var minimalDirectoryMetadataDto in directoryDtoChilds)
                    minimalDirectoryMetadataDto.Permissions =
                        minimalDirectoryMetadataDto.Permissions.FilterHasPermission(requestingUserId);

                // Filter file permissions 
                directory.Files = directory.Files.FilterHasPermission(requestingUserId);

                // Transform to the business object format
                var childDirectories = directoryDtoChilds
                    .Select(x => new MinimalDirectoryMetadata(x))
                    .Cast<IMinimalDirectoryMetadata>()
                    .ToArray();

                // Transform to the business object format
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
