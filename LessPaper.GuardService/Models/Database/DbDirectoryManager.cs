using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
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
        private readonly IMongoTables tables;
        private readonly IMongoClient client;
        private readonly IMongoCollection<DirectoryDto> directoryCollection;
        private readonly IMongoCollection<UserDto> userCollection;
        private readonly IMongoCollection<FileDto> filesCollection;
        private readonly IMongoCollection<FileRevisionDto> fileRevisionCollection;

        public DbDirectoryManager(
            IMongoTables tables,
            IMongoClient client,
            IMongoCollection<DirectoryDto> directoryCollection,
            IMongoCollection<UserDto> userCollection,
            IMongoCollection<FileDto> filesCollection,
            IMongoCollection<FileRevisionDto> fileRevisionCollection)
        {
            this.tables = tables;
            this.client = client;
            this.directoryCollection = directoryCollection;
            this.userCollection = userCollection;
            this.filesCollection = filesCollection;
            this.fileRevisionCollection = fileRevisionCollection;
        }


        /// <inheritdoc />
        public async Task<string[]> Delete(string requestingUserId, string directoryId)
        {
            using var session = await client.StartSessionAsync();
            session.StartTransaction();

            try
            {
                var deleteResult = await directoryCollection.FindOneAndDeleteAsync(session, x =>
                x.Id == directoryId &&
                x.IsRootDirectory == false &&
                x.Permissions.Any(y =>
                    y.Permission.HasFlag(Permission.ReadWrite) &&
                    y.User == new MongoDBRef(tables.UserTable, requestingUserId)
                ));

                // If the result is null permissions are not granted or directory does not exists or is not
                // deletable in the case of the root directory
                if (deleteResult == null)
                {
                    await session.AbortTransactionAsync();
                    return null;
                }

                // Find subdirectories and get a list of all files
                var subDirectories = await directoryCollection
                    .AsQueryable()
                    .Where(x => x.Path.Contains(directoryId))
                    .Select(x => new
                    {
                        Id = x.Id,
                        Files = x.Files
                    })
                    .ToListAsync();

                // Delete subdirectories
                var directoryIds = subDirectories.Select(x => x.Id);
                var deleteDirectoryTask = directoryCollection.DeleteManyAsync(session, x => directoryIds.Contains(x.Id));

                // Delete files
                var fileRefs = subDirectories.SelectMany(x => x.Files).ToArray();
                var fileIds = fileRefs.Select(x => x.Id.AsString);
                var deleteFilesTask = filesCollection.DeleteManyAsync(session, x => fileIds.Contains(x.Id));

                // Get all revisions (in order to remove bucket blobs later on)
                var revisionIds = await fileRevisionCollection
                    .AsQueryable()
                    .Where(x => fileRefs.Contains(x.File))
                    .Select(x => x.Id)
                    .ToListAsync();

                // Delete file revisions
                var deleteRevisionsTask = fileRevisionCollection.DeleteManyAsync(session, x => fileRefs.Contains(x.File));

                await Task.WhenAll(deleteDirectoryTask, deleteFilesTask, deleteRevisionsTask);
                await session.CommitTransactionAsync();

                return revisionIds.ToArray();
            }
            catch (Exception e)
            {
                Console.WriteLine("Error writing to MongoDB: " + e.Message);
                await session.AbortTransactionAsync();
                return null;
            }
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
                            y.User == new MongoDBRef(tables.UserTable, requestingUserId)
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
                    Files = new List<MongoDBRef>(),
                    Permissions = parentDirectory.Permissions,
                    Path = newPath.ToArray(),
                    ParentDirectory = new MongoDBRef(tables.DirectoryTable, parentDirectoryId)
                };

                var insertDirectoryTask = directoryCollection.InsertOneAsync(session, newDirectory);

                var update = Builders<DirectoryDto>.Update
                    .Push(e => e.Directories, new MongoDBRef(tables.DirectoryTable, newDirectoryId));

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
                        y.User == new MongoDBRef(tables.UserTable, requestingUserId)
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
                        y.User == new MongoDBRef(tables.UserTable, requestingUserId)
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

                // Transform to the business object format
                var childDirectories = directoryDtoChilds
                    .Select(x => (IMinimalDirectoryMetadata)new MinimalDirectoryMetadata(x, x.NumberOfChilds))
                    .ToArray();

                var childFiles = new IFileMetadata[0];

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
                  y.User == new MongoDBRef(tables.UserTable, requestingUserId)
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
                         y.User == new MongoDBRef(tables.UserTable, requestingUserId)
                     ) &&

                    // Ensure that we have the parent by checking if parent contains the subfolder that we want to move 
                    x.Directories.Contains(new MongoDBRef(tables.DirectoryTable, objectId)),
                    removeDirectoryRefUpdate);


                // Update filter to add the directory id to the new parent folder
                var addDirectoryRefUpdate = Builders<DirectoryDto>.Update.Push(
                    e => e.Directories,
                    new MongoDBRef(tables.DirectoryTable, objectId));

                var newParentDirectoryTask = directoryCollection.FindOneAndUpdateAsync(
                    session,
                    x =>
                        // Ensure we insert into the right folder
                        x.Id == targetDirectoryId &&

                         // Ensure user has the rights to move the directory into the folder
                         x.Permissions.Any(y =>
                             y.Permission.HasFlag(Permission.ReadWrite) &&
                             y.User == new MongoDBRef(tables.UserTable, requestingUserId)
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
                var parentDirectoryUpdate = Builders<DirectoryDto>.Update.Set(x => x.ParentDirectory,
                    new MongoDBRef(tables.DirectoryTable, targetDirectoryId));

                var mergedUpdate = Builders<DirectoryDto>.Update.Combine(addPathUpdate, parentDirectoryUpdate);
                var added = await directoryCollection.UpdateManyAsync(session, x => x.Path.Contains(objectId), mergedUpdate);

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
        public async Task<IPrepareShareData> PrepareShare(string requestingUserId, string directoryId, string[] userEmails)
        {
            var cancellationToken = new CancellationTokenSource();

            // Start file ids query
            var fileIdsTask = directoryCollection
                .AsQueryable()
                .Where(x => x.Path.Contains(directoryId))
                .SelectMany(x => x.Files)
                .ToListAsync(cancellationToken.Token);

            // Start user information query
            var userDataTask = userCollection.AsQueryable().Where(x => userEmails.Contains(x.Email)).Select(x => new
            {
                Email = x.Email,
                PublicKey = x.PublicKey
            }).ToListAsync(cancellationToken.Token);

            // Check if requesting user has permissions
            var hasPermissionsOnRootFolder = await directoryCollection.AsQueryable().AnyAsync(
                x => x.Id == directoryId &&
                     x.Permissions.Any(y =>
                         y.Permission.HasFlag(Permission.ReadWrite) &&
                         y.User == new MongoDBRef(tables.UserTable, requestingUserId)
                     ));

            if (!hasPermissionsOnRootFolder)
            {
                cancellationToken.Cancel();
                return null;
            }

            // Get file revisions
            var fileIds = await fileIdsTask;
            var revisions = await fileRevisionCollection
                .AsQueryable()
                .Where(x => fileIds.Contains(x.File))
                .Select(x => new
                {
                    Id = x.Id,
                    File = x.File,
                    AccessKeys = x.AccessKeys
                })
                .ToListAsync();

            // Map file revisions to final business object
            var fileKeys = new Dictionary<string, List<IPrepareShareRevision>>();
            foreach (var revision in revisions)
            {
                var accessKey = revision.AccessKeys.FirstOrDefault(x => x.User.Id.AsString == requestingUserId);
                if (accessKey == null)
                    continue;

                var fileId = revision.File.Id.AsString;

                if (!fileKeys.TryGetValue(fileId, out var sharedRevisions))
                    fileKeys.Add(fileId, new List<IPrepareShareRevision>());

                fileKeys[fileId].Add(new PrepareShareRevision(
                    revision.Id, 
                    new[] { accessKey }));
            }
            
            var prepareShareFiles = fileKeys
                .Select(x => (IPrepareShareFile)new PrepareShareFile(x.Key, x.Value.ToArray()))
                .ToArray();

            // Map user data to final business object
            var userData = await userDataTask;
            var userPublicKeys = userData.ToDictionary(x => x.Email, x => x.PublicKey);
            
            return new PrepareShareData(directoryId, userPublicKeys, prepareShareFiles);
        }

        /// <inheritdoc />
        public async Task<bool> Share(string requestingUserId, IShareData shareData)
        {
            var emailAddresses = shareData.EncryptedKeys.Keys;
            var userIds = await userCollection
                .AsQueryable()
                .Where(x => emailAddresses.Contains(x.Email))
                .Select(x => new
                {
                    Id = x.Id,
                    Email = x.Email
                })
                .ToListAsync();

            var mappedValues = userIds.ToDictionary(x => x.Id, x => shareData.EncryptedKeys[x.Email]);

            var models = new List<WriteModel<FileRevisionDto>>();
            foreach (var file in shareData.Files)
            {
                foreach (var revision in file.Revisions)
                {
                    var accessKeys = new List<AccessKeyDto>();
                    foreach (var (userId, accessKey) in revision.AccessKeys)
                    {
                        if (userId != requestingUserId)
                            throw new Exception("Invalid user");

                        accessKeys.Add(new AccessKeyDto
                        {
                            SymmetricEncryptedFileKey = accessKey.SymmetricEncryptedFileKey,
                            Issuer = new MongoDBRef(tables.UserTable, accessKey.IssuerId),
                            User = new MongoDBRef(tables.UserTable, userId)
                        });
                    }

                    var filter = Builders<FileRevisionDto>.Filter.Eq(x => x.Id, revision.RevisionId);
                    var update = Builders<FileRevisionDto>.Update.PushEach(x => x.AccessKeys, accessKeys);

                    var upsertOne = new UpdateOneModel<FileRevisionDto>(filter, update) { IsUpsert = true };
                    models.Add(upsertOne);
                }
            }

            var e = await fileRevisionCollection.BulkWriteAsync(models);

            return true;
        }
    }
}
