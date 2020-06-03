using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using LessPaper.Guard.Database.MongoDb.Interfaces;
using LessPaper.Guard.Database.MongoDb.Models.Dtos;
using LessPaper.Guard.Database.MongoDb.Models.Helper;
using LessPaper.Guard.Database.MongoDb.Models.Implement;
using LessPaper.Shared.Enums;
using LessPaper.Shared.Interfaces.Database.Manager;
using LessPaper.Shared.Interfaces.General;
using LessPaper.Shared.Interfaces.GuardApi.Response;
using LessPaper.Shared.Models.Exceptions;
using MongoDB.Driver;
using MongoDB.Driver.Linq;

namespace LessPaper.Guard.Database.MongoDb.Models
{
    public class DbDirectoryManager : IDbDirectoryManager
    {
        private readonly IMongoClient client;
        private readonly IMongoCollection<DirectoryDto> directoryCollection;
        private readonly IMongoCollection<UserDto> userCollection;
        private readonly IMongoCollection<FileDto> filesCollection;
        private readonly IMongoCollection<FileRevisionDto> fileRevisionCollection;

        public DbDirectoryManager(IDatabaseManager dbManager)
        {
            client = dbManager.Client;
            directoryCollection = dbManager.DirectoryCollection;
            userCollection = dbManager.UserCollection;
            filesCollection = dbManager.FilesCollection;
            fileRevisionCollection = dbManager.FileRevisionCollection;
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
                    y.UserId == requestingUserId
                ));

                // If the result is null permissions are not granted or directory does not exists or is not
                // deletable in the case of the root directory
                if (deleteResult == null)
                {
                    throw new ObjectNotResolvableException(
                        $"Deletion of directory {directoryId} could not be performed by user {requestingUserId}");
                }

                // Find subdirectories and get a list of all files
                var subDirectories = await directoryCollection
                    .AsQueryable()
                    .Where(x => x.PathIds.Contains(directoryId))
                    .Select(x => new
                    {
                        Id = x.Id,
                        Files = x.FileIds
                    })
                    .ToListAsync();

                // Delete subdirectories
                var directoryIds = subDirectories.Select(x => x.Id);
                var deleteDirectoryTask = directoryCollection.DeleteManyAsync(session, x => directoryIds.Contains(x.Id));

                // Delete files
                var fileIds = subDirectories.SelectMany(x => x.Files).ToArray();

                var deleteFilesTask = filesCollection.DeleteManyAsync(session, x => fileIds.Contains(x.Id));

                // Get all revisions (in order to remove bucket blobs later on)
                var revisionIds = await fileRevisionCollection
                    .AsQueryable()
                    .Where(x => fileIds.Contains(x.File))
                    .Select(x => x.Id)
                    .ToListAsync();

                // Delete file revisions
                var deleteRevisionsTask = fileRevisionCollection.DeleteManyAsync(session, x => fileIds.Contains(x.File));

                await Task.WhenAll(deleteDirectoryTask, deleteFilesTask, deleteRevisionsTask);
                await session.CommitTransactionAsync();

                return revisionIds.ToArray();
            }
            catch (ObjectNotResolvableException)
            {
                await session.AbortTransactionAsync();
                throw;
            }
            catch (InvalidParameterException)
            {
                await session.AbortTransactionAsync();
                throw;
            }
            catch (MongoException e)
            {
                await session.AbortTransactionAsync();
                throw new DatabaseException("Database error during file inserting. See inner exception.", e);
            }
            catch (Exception e)
            {
                await session.AbortTransactionAsync();
                throw new Exception("Unknown error during file inserting. See inner exception.", e);
            }
        }

        /// <inheritdoc />
        public async Task<bool> InsertDirectory(string requestingUserId, string parentDirectoryId, string directoryName, string newDirectoryId)
        {
            using var session = await client.StartSessionAsync();

            try
            {
                session.StartTransaction();

                var parentDirectory = await directoryCollection.AsQueryable()
                    .Where(x =>
                        x.Id == parentDirectoryId &&
                        x.Permissions.Any(y =>
                            y.Permission.HasFlag(Permission.ReadWrite) &&
                            y.UserId == requestingUserId
                        ))
                    .Select(x => new
                    {
                        Owner = x.OwnerId,
                        Permissions = x.Permissions,
                        Path = x.PathIds
                    })
                    .FirstOrDefaultAsync();

                if (parentDirectory == null)
                {
                    throw new ObjectNotResolvableException(
                        $"Parent directory {parentDirectoryId} could not be found or accessed by user {requestingUserId} during directory insertion");
                }

                var newPath = parentDirectory.Path.ToList();
                newPath.Add(newDirectoryId);

                var newDirectory = new DirectoryDto
                {
                    Id = newDirectoryId,
                    IsRootDirectory = false,
                    OwnerId = parentDirectory.Owner,
                    ObjectName = directoryName,
                    DirectoryIds = new List<string>(),
                    FileIds = new List<string>(),
                    Permissions = parentDirectory.Permissions,
                    PathIds = newPath.ToArray(),
                    ParentDirectoryId = parentDirectoryId
                };

                var insertDirectoryTask = directoryCollection.InsertOneAsync(session, newDirectory);

                var update = Builders<DirectoryDto>.Update.Push(e => e.DirectoryIds, newDirectoryId);
                var updateResultTask = directoryCollection.UpdateOneAsync(session, x => x.Id == parentDirectoryId, update);

                await Task.WhenAll(insertDirectoryTask, updateResultTask);
                if (updateResultTask.Result.ModifiedCount != 1)
                {
                    throw new UnexpectedBehaviourException(
                        $"Insertion of new directory {newDirectoryId} to {parentDirectoryId} changed {updateResultTask.Result.ModifiedCount} parent directories but must exactly change 1");
                }

                await session.CommitTransactionAsync();
                return true;
            }
            catch (ObjectNotResolvableException)
            {
                await session.AbortTransactionAsync();
                throw;
            }
            catch (InvalidParameterException)
            {
                await session.AbortTransactionAsync();
                throw;
            }
            catch (UnexpectedBehaviourException)
            {
                await session.AbortTransactionAsync();
                throw;
            }
            catch (MongoException e)
            {
                await session.AbortTransactionAsync();
                throw new DatabaseException("Database error during file inserting. See inner exception.", e);
            }
            catch (Exception e)
            {
                await session.AbortTransactionAsync();
                throw new Exception("Unknown error during file inserting. See inner exception.", e);
            }
        }

        /// <inheritdoc />
        public async Task<IPermissionResponse[]> GetPermissions(string requestingUserId, string userId, string[] objectIds)
        {
            try
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
                            y.UserId == requestingUserId
                        ))
                    .Select(x => new
                    {
                        Id = x.Id,
                        Permissions = x.Permissions
                    })
                    .ToListAsync();

                if (directoryPermissions.Count == 0)
                {
                    throw new ObjectNotResolvableException(
                        $"Directories could not be found or accessed by user {requestingUserId} during directory get permissions request");
                }

                var responseObj = new List<IPermissionResponse>(directoryPermissions.Count);
                if (requestingUserId == userId)
                {
                    // Require at least a read flag
                    responseObj.AddRange((from directoryPermission in directoryPermissions
                                          let permissionEntry = directoryPermission.Permissions
                                              .RestrictPermissions(requestingUserId)
                                              .FirstOrDefault()
                                          where permissionEntry != null
                                          select new PermissionResponse(directoryPermission.Id, permissionEntry.Permission)));
                }
                else
                {
                    // Require ReadPermissions flag
                    responseObj.AddRange((from directoryPermission in directoryPermissions
                                          let permissionEntry = directoryPermission.Permissions
                                              .RestrictPermissions(requestingUserId)
                                              .FirstOrDefault(x => x.UserId == userId)
                                          where permissionEntry != null
                                          select new PermissionResponse(directoryPermission.Id, permissionEntry.Permission)));
                }


                return responseObj.ToArray();
            }
            catch (ObjectNotResolvableException)
            {
                throw;
            }
            catch (InvalidParameterException)
            {
                throw;
            }
            catch (MongoException e)
            {
                throw new DatabaseException("Database error during get directory permission request. See inner exception.", e);
            }
            catch (Exception e)
            {
                throw new Exception("Unknown error during get directory permission request. See inner exception.", e);
            }
        }

        /// <inheritdoc />
        public async Task<IDirectoryMetadata> GetDirectoryMetadata(string requestingUserId, string directoryId, uint? revisionNumber)
        {
            try
            {
                // [Optimistic execution] Receive the files and the revisions
                var getRawFileChildsTask = filesCollection
                    .AsQueryable()
                    .Where(x => x.ParentDirectoryId == directoryId)
                    .GroupJoin(
                        fileRevisionCollection,
                        keySel => keySel.Id,
                        innerSel => innerSel.File,
                        (file, revisions) => new
                        {
                            File = file,
                            Revisons = revisions
                        })
                    .ToListAsync();

                // Get the directory
                var directory = await directoryCollection.Find(x =>
                    x.Id == directoryId &&
                    x.Permissions.Any(y =>
                        y.Permission.HasFlag(Permission.Read) &&
                        y.UserId == requestingUserId
                    )).FirstOrDefaultAsync();

                // Return null if permissions not granted or directory does not exists
                if (directory == null)
                {
                    throw new ObjectNotResolvableException(
                        $"Directory {directoryId} could not be found or accessed by user {requestingUserId} during directory get directory metadata request");
                }


                // Get Child directories
                var childDirectoryIds = directory.DirectoryIds.ToArray();
                var directoryDtoChildsTask = directoryCollection
                    .AsQueryable()
                    .Where(x => childDirectoryIds.Contains(x.Id))
                    .Select(x => new MinimalDirectoryMetadataDto()
                    {
                        NumberOfChilds = (uint)(x.FileIds.Count + x.DirectoryIds.Count),
                        Id = x.Id,
                        ObjectName = x.ObjectName,
                        Permissions = x.Permissions,
                        PathIds = x.PathIds
                    }).ToListAsync();

                await Task.WhenAll(directoryDtoChildsTask, getRawFileChildsTask);

                // Filter directory permissions
                directory.Permissions = directory.Permissions.RestrictPermissions(requestingUserId);

                // Filter child directory permissions
                foreach (var minimalDirectoryMetadataDto in directoryDtoChildsTask.Result)
                    minimalDirectoryMetadataDto.Permissions =
                        minimalDirectoryMetadataDto.Permissions.RestrictPermissions(requestingUserId);

                // Transform to the business object format
                var childDirectories = directoryDtoChildsTask.Result
                    .Select(x => (IMinimalDirectoryMetadata)new MinimalDirectoryMetadata(x, x.NumberOfChilds))
                    .ToArray();

                var childFiles = getRawFileChildsTask.Result
                    .Select(x => (IFileMetadata)new File(
                        x.File.RestrictPermissions(requestingUserId),
                        x.Revisons.ToArray())
                    )
                    .ToArray();

                return new Directory(directory, childFiles, childDirectories);
            }
            catch (ObjectNotResolvableException)
            {
                throw;
            }
            catch (InvalidParameterException)
            {
                throw;
            }
            catch (MongoException e)
            {
                throw new DatabaseException("Database error during get directory metadata request. See inner exception.", e);
            }
            catch (Exception e)
            {
                throw new Exception("Unknown error during get directory metadata request. See inner exception.", e);
            }
        }

        /// <inheritdoc />
        public async Task<bool> Rename(string requestingUserId, string directoryId, string newName)
        {
            using var session = await client.StartSessionAsync();

            try
            {
                session.StartTransaction();

                var update = Builders<DirectoryDto>.Update.Set(x => x.ObjectName, newName);
                var updateResultTask = await directoryCollection.UpdateOneAsync(session, x =>
                    x.Id == directoryId &&
                    x.Permissions.Any(y =>
                        y.Permission.HasFlag(Permission.ReadWrite) &&
                        y.UserId == requestingUserId
                    ), update);

                if (updateResultTask.ModifiedCount == 0)
                {
                    throw new ObjectNotResolvableException(
                        $"Directory {directoryId} could not be found or accessed by user {requestingUserId} during directory rename");
                }

                if (updateResultTask.ModifiedCount != 1)
                {
                    throw new UnexpectedBehaviourException(
                        $"Rename directory {directoryId} by user {requestingUserId} modified to many elements");
                }

                await session.CommitTransactionAsync();
                return true;
            }
            catch (ObjectNotResolvableException)
            {
                await session.AbortTransactionAsync();
                throw;
            }
            catch (InvalidParameterException)
            {
                await session.AbortTransactionAsync();
                throw;
            }
            catch (UnexpectedBehaviourException)
            {
                await session.AbortTransactionAsync();
                throw;
            }
            catch (MongoException e)
            {
                await session.AbortTransactionAsync();
                throw new DatabaseException("Database error during directory rename. See inner exception.", e);
            }
            catch (Exception e)
            {
                await session.AbortTransactionAsync();
                throw new Exception("Unknown error during directory rename. See inner exception.", e);
            }
        }

        /// <inheritdoc />
        public async Task<bool> Move(string requestingUserId, string objectId, string targetDirectoryId)
        {
            using var session = await client.StartSessionAsync();

            try
            {
                session.StartTransaction();

                // Update filter to remove the directory id from the old parent folder
                var removeDirectoryRefUpdate = Builders<DirectoryDto>.Update.Pull(x => x.DirectoryIds, objectId);

                var oldParentDirectoryTask = directoryCollection.FindOneAndUpdateAsync(
                    session,
                x =>
                    // Ensure old folder is not the new folder
                    x.Id != targetDirectoryId &&

                     // Ensure user has the rights to move the folder out of the old parent directory
                     x.Permissions.Any(y =>
                         y.Permission.HasFlag(Permission.ReadWrite) &&
                         y.UserId == requestingUserId
                     ) &&

                    // Ensure that we have the parent by checking if parent contains the subfolder that we want to move 
                    x.DirectoryIds.Contains(objectId),
                    removeDirectoryRefUpdate);

                // Update filter to add the directory id to the new parent folder
                var addDirectoryRefUpdate = Builders<DirectoryDto>.Update.Push(
                    e => e.DirectoryIds,
                    objectId);

                var newParentDirectoryTask = directoryCollection.FindOneAndUpdateAsync(
                    session,
                    x =>
                         // Ensure we insert into the right folder
                         x.Id == targetDirectoryId &&
                         // Ensure user has the rights to move the directory into the folder
                         x.Permissions.Any(y =>
                             y.Permission.HasFlag(Permission.ReadWrite) &&
                             y.UserId == requestingUserId
                         ),
                    addDirectoryRefUpdate);

                // Wait for both move operations
                await Task.WhenAll(oldParentDirectoryTask, newParentDirectoryTask);

                // Ensure directories could be resolved
                if (oldParentDirectoryTask.Result == null)
                {
                    throw new ObjectNotResolvableException(
                        $"Parent directory of directory {objectId} could not be found or accessed by user {requestingUserId} during directory move");
                }

                if (newParentDirectoryTask.Result == null)
                {
                    throw new ObjectNotResolvableException(
                        $"New parent directory {targetDirectoryId} could not be found or accessed by user {requestingUserId} during directory move");
                }

                // Going to update the new directory path

                // Remove part of old path (excluding the directory-to-move id)
                var oldPath = oldParentDirectoryTask.Result.PathIds.ToList();

                // Apply for directories
                var directoryRemovePathSegmentsUpdate = Builders<DirectoryDto>.Update.PullAll(x => x.PathIds, oldPath);
                var removedChangedDirectoryDtosTask = directoryCollection.UpdateManyAsync(
                         session,
                    x => x.PathIds.Contains(objectId),
                         directoryRemovePathSegmentsUpdate
                );

                // Apply for files
                var filesRemovePathSegmentsUpdate = Builders<FileDto>.Update.PullAll(x => x.PathIds, oldPath);
                var removeChangedFileDtosTask = filesCollection.UpdateManyAsync(
                    session,
                    x => x.PathIds.Contains(objectId),
                    filesRemovePathSegmentsUpdate
                );

                // Wait for path update for all files and directories
                await Task.WhenAll(removedChangedDirectoryDtosTask, removeChangedFileDtosTask);

                // Add new path (excluding the directory-to-move id)
                var newPath = newParentDirectoryTask.Result.PathIds.ToList();

                // Apply for directories
                var directoriesAddPathSegmentsUpdate = Builders<DirectoryDto>.Update.PushEach(x => x.PathIds, newPath, position: 0);
                var directoriesParentDirectoryUpdate = Builders<DirectoryDto>.Update.Set(
                    x => x.ParentDirectoryId, targetDirectoryId);

                var directoriesMergedUpdate = Builders<DirectoryDto>.Update.Combine(directoriesAddPathSegmentsUpdate, directoriesParentDirectoryUpdate);
                var addChangedDirectoryDtosTask = directoryCollection.UpdateManyAsync(
                        session,
                        x => x.PathIds.Contains(objectId),
                        directoriesMergedUpdate
                );

                // Apply for files
                var filesAddPathSegmentsUpdate = Builders<FileDto>.Update.PushEach(x => x.PathIds, newPath, position: 0);
                var filesParentDirectoryUpdate = Builders<FileDto>.Update.Set(
                    x => x.ParentDirectoryId,
                     targetDirectoryId
                );

                var filesMergedUpdate = Builders<FileDto>.Update.Combine(filesAddPathSegmentsUpdate, filesParentDirectoryUpdate);
                var addChangedFilesDtosTask = filesCollection.UpdateManyAsync(
                    session,
                    x => x.PathIds.Contains(objectId),
                    filesMergedUpdate
                );

                // Wait for path update for all files and directories
                await Task.WhenAll(addChangedDirectoryDtosTask, addChangedFilesDtosTask);

                // Ensure same amount of documents are modified 
                if (removedChangedDirectoryDtosTask.Result.ModifiedCount != addChangedDirectoryDtosTask.Result.ModifiedCount ||
                    removeChangedFileDtosTask.Result.ModifiedCount != addChangedFilesDtosTask.Result.ModifiedCount)
                {
                    throw new UnexpectedBehaviourException(
                        $"Move directory {objectId} to directory {targetDirectoryId} by user {requestingUserId} modified to many elements");
                }

                await session.CommitTransactionAsync();
                return true;
            }
            catch (ObjectNotResolvableException)
            {
                await session.AbortTransactionAsync();
                throw;
            }
            catch (UnexpectedBehaviourException)
            {
                await session.AbortTransactionAsync();
                throw;
            }
            catch (MongoException e)
            {
                await session.AbortTransactionAsync();
                throw new DatabaseException("Database error during directory move. See inner exception.", e);
            }
            catch (Exception e)
            {
                await session.AbortTransactionAsync();
                throw new Exception("Unknown error during directory move. See inner exception.", e);
            }
        }

        /// <inheritdoc />
        public async Task<IPrepareShareData> PrepareShare(string requestingUserId, string directoryId, string[] userEmails)
        {
            try
            {
                var cancellationToken = new CancellationTokenSource();

                // Start file ids query
                var fileIdsTask = directoryCollection
                    .AsQueryable()
                    .Where(x => x.PathIds.Contains(directoryId))
                    .SelectMany(x => x.FileIds)
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
                             y.UserId == requestingUserId
                         ));

                if (!hasPermissionsOnRootFolder)
                {
                    cancellationToken.Cancel();
                    throw new ObjectNotResolvableException(
                        $"Directory {directoryId} could not be found or accessed by user {requestingUserId} during share preparation");
                }

                // TODO Aggerate pipeline

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
                    var accessKey = revision.AccessKeys.FirstOrDefault(x => x.UserId == requestingUserId);
                    if (accessKey == null)
                        continue;

                    var fileId = revision.File;

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
            catch (ObjectNotResolvableException)
            {
                throw;
            }
            catch (MongoException e)
            {
                throw new DatabaseException("Database error during directory prepare share. See inner exception.", e);
            }
            catch (Exception e)
            {
                throw new Exception("Unknown error during directory prepare share. See inner exception.", e);
            }
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
                    foreach (var kv in revision.AccessKeys)
                    {
                        var userId = kv.Key;
                        var accessKey = kv.Value;

                        if (userId != requestingUserId)
                            throw new Exception("Invalid user");

                        accessKeys.Add(new AccessKeyDto
                        {
                            SymmetricEncryptedFileKey = accessKey.SymmetricEncryptedFileKey,
                            IssuerId = accessKey.IssuerId,
                            UserId = userId
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
