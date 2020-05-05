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
using MongoDB.Driver.Core.Operations;
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
        public async Task<string[]> Delete(string requestingUserId, string directoryId)
        {
            var deleteResult = await directoryCollection.FindOneAndDeleteAsync(x =>
                x.Id == directoryId &&
                x.IsRootDirectory == false &&
                x.Permissions.Any(y =>
                    y.Permission.HasFlag(Permission.ReadWrite) &&
                    y.User == new MongoDBRef("user", requestingUserId)
                ));

            return deleteResult?.Files.SelectMany(x => x.Revisions).Select(x => x.BlobId).ToArray();
        }

        /// <inheritdoc />
        public async Task<bool> InsertDirectory(string requestingUserId, string parentDirectoryId, string directoryName, string newDirectoryId)
        {
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
                        Permissions = x.Permissions,
                        Path = x.Path
                    })
                    .FirstOrDefaultAsync();

                if (parentDirectory == null)
                    return false;

                var newPath = parentDirectory.Path.ToList();
                newPath.Add(newDirectoryId);

                var newDirectory = new DirectoryDto
                {
                    Id = newDirectoryId,
                    IsRootDirectory = false,
                    Owner = parentDirectory.Owner,
                    ObjectName = directoryName,
                    Directories = new List<MongoDBRef>(),
                    Files = new List<FileDto>(),
                    Permissions = parentDirectory.Permissions,
                    Path = newPath.ToArray()
                };

                var insertDirectoryTask = directoryCollection.InsertOneAsync(session, newDirectory);

                var update = Builders<DirectoryDto>.Update
                    .Push(e => e.Directories, new MongoDBRef("directories", newDirectoryId));

                var updateResultTask = directoryCollection.UpdateOneAsync(session, x => x.Id == parentDirectoryId, update);

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
        public async Task<IPermissionResponse[]> GetPermissions(string requestingUserId, string userId, string[] objectIds)
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
                                          .RestrictPermissions(requestingUserId)
                                          .FirstOrDefault()
                                      where permissionEntry != null
                                      select new PermissionResponse(directoryPermission.Id, permissionEntry.Permission)).Cast<IPermissionResponse>());
            }
            else
            {
                // Require ReadPermissions flag
                responseObj.AddRange((from directoryPermission in directoryPermissions
                                      let permissionEntry = directoryPermission.Permissions
                                          .RestrictPermissions(requestingUserId)
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
                        Id = x.Id,
                        ObjectName = x.ObjectName,
                        Permissions = x.Permissions
                    }).ToListAsync();

                // Filter directory permissions
                directory.Permissions = directory.Permissions.RestrictPermissions(requestingUserId);

                // Filter child directory permissions
                foreach (var minimalDirectoryMetadataDto in directoryDtoChilds)
                    minimalDirectoryMetadataDto.Permissions =
                        minimalDirectoryMetadataDto.Permissions.RestrictPermissions(requestingUserId);

                // Filter child files permissions 
                directory.Files = directory.Files.RestrictPermissions(requestingUserId);
                directory.Files = directory.Files.RestrictAccessKeys(requestingUserId);

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

        /// <inheritdoc />
        public async Task<bool> Rename(string requestingUserId, string objectId, string newName)
        {
            var update = Builders<DirectoryDto>.Update.Set(x => x.ObjectName, newName);

            var updateResultTask = await directoryCollection.UpdateOneAsync(x =>
                x.Id == objectId &&
                x.Permissions.Any(y =>
                    y.Permission.HasFlag(Permission.ReadWrite) &&
                  y.User == new MongoDBRef("user", requestingUserId)
            ), update);

            return updateResultTask.ModifiedCount == 1;
        }

        /// <inheritdoc />
        public async Task<bool> Move(string requestingUserId, string objectId, string targetDirectoryId)
        {
            using var session = await client.StartSessionAsync();
            session.StartTransaction();

            try
            {
                // Update filter to remove the directory id from the old parent folder
                var removeDirectoryRefUpdate = Builders<DirectoryDto>.Update.PullFilter(
                    x => x.Directories,
                    Builders<MongoDBRef>.Filter.Eq(x => x.Id, new BsonString(objectId)));

                var oldParentDirectoryTask = directoryCollection.FindOneAndUpdateAsync(
                    session,
                x => 
                    // Ensure old folder is not the new folder
                    x.Id != targetDirectoryId &&

                    // Ensure user has the rights to move the folder out of the old parent directory
                     x.Permissions.Any(y =>
                         y.Permission.HasFlag(Permission.ReadWrite) &&
                         y.User == new MongoDBRef("user", requestingUserId)
                     ) &&

                    // Ensure that we have the parent by checking if parent contains the subfolder that we want to move 
                    x.Directories.Contains(new MongoDBRef("directories", objectId)),
                    removeDirectoryRefUpdate);


                // Update filter to add the directory id to the new parent folder
                var addDirectoryRefUpdate = Builders<DirectoryDto>.Update.Push(
                    e => e.Directories, 
                    new MongoDBRef("directories", objectId));

                var newParentDirectoryTask = directoryCollection.FindOneAndUpdateAsync(
                    session, 
                    x => 
                        // Ensure we insert into the right folder
                        x.Id == targetDirectoryId &&

                         // Ensure user has the rights to move the directory into the folder
                         x.Permissions.Any(y =>
                             y.Permission.HasFlag(Permission.ReadWrite) &&
                             y.User == new MongoDBRef("user", requestingUserId)
                         ),
                    addDirectoryRefUpdate);

                // Wait for both move operations
                await Task.WhenAll(oldParentDirectoryTask, newParentDirectoryTask);

                // Ensure directories could be resolved
                if (oldParentDirectoryTask.Result == null || newParentDirectoryTask.Result == null)
                {
                    await session.AbortTransactionAsync();
                    return false;
                }

                // Going to update the new directory path

                // Remove part of old path
                var oldPath = oldParentDirectoryTask.Result.Path.ToList();
                var removePathUpdate = Builders<DirectoryDto>.Update.PullAll(x => x.Path, oldPath);
                var removed = await directoryCollection.UpdateManyAsync(session, x => x.Path.Contains(objectId), removePathUpdate);

                // Add new path
                var newPath = newParentDirectoryTask.Result.Path.ToList();
                var addPathUpdate = Builders<DirectoryDto>.Update.PushEach(x => x.Path, newPath, position: 0);
                var added = await directoryCollection.UpdateManyAsync(session, x => x.Path.Contains(objectId), addPathUpdate);

                // Ensure same amount of documents are modified 
                if (removed.ModifiedCount != added.ModifiedCount)
                {
                    await session.AbortTransactionAsync();
                    return false;
                }

                await session.CommitTransactionAsync();
                return true;
            }
            catch (Exception e)
            {
                Console.WriteLine("Error writing to MongoDB: " + e.Message);
                await session.AbortTransactionAsync();
                return false;
            }
        }

        /// <inheritdoc />
        public async Task<PrepareShareData[]> PrepareShare(string requestingUserId, string objectId, string[] userEmails)
        {
            var hasPermissionsOnRootFolder = await directoryCollection.AsQueryable().AnyAsync(
                x => x.Id == objectId &&
                     x.Permissions.Any(y =>
                         y.Permission.HasFlag(Permission.ReadWrite) &&
                         y.User == new MongoDBRef("user", requestingUserId)
                     ));

            if (!hasPermissionsOnRootFolder)
                return null;

            var files = await directoryCollection.AsQueryable().Where(x => x.Path.Contains(objectId))
                .SelectMany(x => x.Files)
                .SelectMany(x => x.Revisions)
                .Select(x => new
                {
                    Id = x.BlobId,
                    AccessKeys = x.AccessKeys
                }).ToListAsync();

            return null;
        }

        /// <inheritdoc />
        public async Task<bool> Share(string requestingUserId, ShareData[] shareData)
        {
            throw new NotImplementedException();
        }
    }
}
