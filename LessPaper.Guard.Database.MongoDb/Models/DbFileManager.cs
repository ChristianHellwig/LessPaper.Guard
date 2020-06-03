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
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Driver;
using MongoDB.Driver.Linq;

namespace LessPaper.Guard.Database.MongoDb.Models
{
    public class DbFileManager : IDbFileManager
    {
        private readonly IMongoClient client;
        private readonly IMongoCollection<DirectoryDto> directoryCollection;
        private readonly IMongoCollection<UserDto> userCollection;
        private readonly IMongoCollection<FileDto> filesCollection;
        private readonly IMongoCollection<FileRevisionDto> fileRevisionCollection;

        public DbFileManager(IDatabaseManager dbManager)
        {
            client = dbManager.Client;
            directoryCollection = dbManager.DirectoryCollection;
            userCollection = dbManager.UserCollection;
            filesCollection = dbManager.FilesCollection;
            fileRevisionCollection = dbManager.FileRevisionCollection;
        }


        private class TempRevisionView
        {
            public List<string> RevisionIds { get; set; }

            [BsonId]
            public string Id { get; set; }
        }

        /// <inheritdoc />
        public async Task<string[]> Delete(string requestingUserId, string fileId)
        {
            using var session = await client.StartSessionAsync();

            try
            {
                session.StartTransaction();

                var directoryUpdate = Builders<DirectoryDto>.Update.Pull(x => x.FileIds, fileId);
                var updatedDirectoryTask = await directoryCollection.UpdateOneAsync(session,
                    x =>
                        x.FileIds.Contains(fileId) &&
                        (x.OwnerId == requestingUserId ||
                         x.Permissions.Any(y =>
                            y.Permission.HasFlag(Permission.ReadWrite) &&
                            y.UserId == requestingUserId
                        )),
                    directoryUpdate);


                if (updatedDirectoryTask.ModifiedCount == 0)
                {
                    throw new ObjectNotResolvableException(
                        $"Parent directory of file {fileId} could not be found or accessed by user {requestingUserId} during file delete");
                }

                if (updatedDirectoryTask.ModifiedCount > 1)
                {
                    throw new UnexpectedBehaviourException(
                        $"Delete of file {fileId} by user {requestingUserId} changed {updatedDirectoryTask.ModifiedCount} parent directories but must exactly change 1");
                }

                var revisionsTask = fileRevisionCollection.DeleteManyAsync(session, x => x.File == fileId);
                var deleteFileTask = filesCollection.FindOneAndDeleteAsync(
                    session,
                    x => x.Id == fileId, new FindOneAndDeleteOptions<FileDto, TempRevisionView>
                    {
                        Projection = Builders<FileDto>.Projection.Include(x => x.RevisionIds)
                    });

                await Task.WhenAll(deleteFileTask, revisionsTask);
                await session.CommitTransactionAsync();

                return deleteFileTask.Result.RevisionIds.ToArray();
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
                throw new DatabaseException("Database error during file delete. See inner exception.", e);
            }
            catch (Exception e)
            {
                await session.AbortTransactionAsync();
                throw new Exception("Unknown error during file delete. See inner exception.", e);
            }
        }

        /// <inheritdoc />
        public async Task<IPermissionResponse[]> GetPermissions(string requestingUserId, string userId, string[] objectIds)
        {
            try
            {
                var files = await filesCollection.AsQueryable()
                    .Where(x =>
                        objectIds.Contains(x.Id) &&
                        (x.OwnerId == requestingUserId ||
                         x.Permissions.Any(y =>
                             (y.Permission.HasFlag(Permission.Read) ||
                              y.Permission.HasFlag(Permission.ReadPermissions)) &&
                             y.UserId == requestingUserId
                         )))
                    .Select(x => new
                    {
                        Id = x.Id,
                        Permissions = x.Permissions
                    })
                    .ToListAsync();

                // Restrict response to relevant and viewable permissions
                var responseObj = new List<IPermissionResponse>(files.Count);
                if (requestingUserId == userId)
                {
                    // Require at least a read flag
                    responseObj.AddRange((files
                        .Select(directoryPermission => new
                        {
                            directoryPermission,
                            permissionEntry = directoryPermission.Permissions
                                .RestrictPermissions(requestingUserId)
                                .FirstOrDefault()
                        })
                        .Where(x => x.permissionEntry != null)
                        .Select(x => new PermissionResponse(x.directoryPermission.Id, x.permissionEntry.Permission))));
                }
                else
                {
                    responseObj.AddRange(files
                        .Select(directoryPermission => new
                        {
                            directoryPermission,
                            permissionEntry = directoryPermission.Permissions
                                .RestrictPermissions(requestingUserId)
                                .FirstOrDefault(x => x.UserId == userId)
                        })
                        .Where(x => x.permissionEntry != null)
                        .Select(x => new PermissionResponse(x.directoryPermission.Id, x.permissionEntry.Permission)));
                }

                return responseObj.ToArray();
            }
            catch (MongoException e)
            {
                throw new DatabaseException("Database error during directory move. See inner exception.", e);
            }
            catch (Exception e)
            {
                throw new Exception("Unknown error during directory move. See inner exception.", e);
            }
        }

        /// <inheritdoc />
        public async Task<bool> Rename(string requestingUserId, string fileId, string newName)
        {
            using var session = await client.StartSessionAsync();

            try
            {
                session.StartTransaction();
                
                var update = Builders<FileDto>.Update.Set(x => x.ObjectName, newName);
                var updateResult = await filesCollection.UpdateOneAsync(
                    session,
                x =>
                        x.Id == fileId &&
                        (x.OwnerId == requestingUserId ||
                         x.Permissions.Any(y =>
                             y.Permission.HasFlag(Permission.ReadWrite) &&
                             y.UserId == requestingUserId
                         )), update);
                
                if (updateResult.ModifiedCount == 0)
                {
                    throw new ObjectNotResolvableException(
                        $"File {fileId} could not be found or accessed by user {requestingUserId} during file rename");
                }

                if (updateResult.ModifiedCount > 1)
                {
                    throw new UnexpectedBehaviourException(
                        $"Rename of file {fileId} by user {requestingUserId} changed {updateResult.ModifiedCount} files but must exactly change 1");
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
                throw new DatabaseException("Database error during file rename. See inner exception.", e);
            }
            catch (Exception e)
            {
                await session.AbortTransactionAsync();
                throw new Exception("Unknown error during file rename. See inner exception.", e);
            }
        }

        /// <inheritdoc />
        public async Task<bool> Move(string requestingUserId, string fileId, string targetDirectoryId)
        {
            using var session = await client.StartSessionAsync();
            var cancellationTokenSource = new CancellationTokenSource();

            try
            {
                session.StartTransaction();

                var popUpdate = Builders<DirectoryDto>.Update.Pull(x => x.FileIds, fileId);

                // Remove file from directory
                var removeFileFromOldDirectoryTask = directoryCollection.UpdateOneAsync(session, x =>
                    x.Id != targetDirectoryId &&
                    x.FileIds.Contains(fileId) &&
                    (x.OwnerId == requestingUserId ||
                     x.Permissions.Any(y =>
                         y.Permission.HasFlag(Permission.ReadWrite) &&
                         y.UserId == requestingUserId
                     )), popUpdate, cancellationToken: cancellationTokenSource.Token);

                // Add file to directory
                var addUpdate =
                    Builders<DirectoryDto>.Update.Push(x => x.FileIds, fileId);

                var addFileToNewDirectory = await directoryCollection.FindOneAndUpdateAsync(session, x =>
                    x.Id == targetDirectoryId &&
                    (x.OwnerId == requestingUserId ||
                     x.Permissions.Any(y =>
                         y.Permission.HasFlag(Permission.ReadWrite) &&
                         y.UserId == requestingUserId
                     )), addUpdate, cancellationToken: cancellationTokenSource.Token);

                // Cancel if the new directory could not be resolved
                if (addFileToNewDirectory == null)
                {
                    cancellationTokenSource.Cancel();
                    //await removeFileFromOldDirectoryTask;
                    throw new ObjectNotResolvableException(
                        $"File {fileId} could not be found or accessed by user {requestingUserId} during file move");
                }


                // Update parent directory of file
                var fileParentDirectoryUpdate = Builders<FileDto>.Update.Set(x => x.ParentDirectoryId, targetDirectoryId);

                // Update file path
                var newPath = addFileToNewDirectory.PathIds.ToList();
                newPath.Add(fileId);
                var filePathUpdate = Builders<FileDto>.Update.Set(x => x.PathIds, newPath.ToArray());

                // Update permissions to directory permissions
                var filePermissionsUpdate =
                    Builders<FileDto>.Update.Set(x => x.Permissions, addFileToNewDirectory.Permissions);

                var fileUpdate = Builders<FileDto>.Update.Combine(fileParentDirectoryUpdate, filePermissionsUpdate, filePathUpdate);
                var updateFile = await filesCollection.FindOneAndUpdateAsync(session, x => x.Id == fileId, fileUpdate);

                //TODO RevisionIds need the key if a file is moved in a directory with new people


                // Wait for tasks
                await Task.WhenAll(removeFileFromOldDirectoryTask);

                // Check if all tasks modified exactly one file
                if (removeFileFromOldDirectoryTask.Result.ModifiedCount != 1 ||
                    updateFile == null)
                {
                    throw new UnexpectedBehaviourException(
                        $"Move of file {fileId} by user {requestingUserId} changed {removeFileFromOldDirectoryTask.Result.ModifiedCount} directories but must exactly change 1");
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
                throw new DatabaseException("Database error during file rename. See inner exception.", e);
            }
            catch (Exception e)
            {
                await session.AbortTransactionAsync();
                throw new Exception("Unknown error during file rename. See inner exception.", e);
            }
        }

        /// <inheritdoc />
        public async Task<IPrepareShareData> PrepareShare(string requestingUserId, string objectId, string[] userEmails)
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc />
        public async Task<bool> Share(string requestingUserId, IShareData shareData)
        {
            throw new NotImplementedException();
        }


        /// <inheritdoc />
        public async Task<uint> InsertFile(
            string requestingUserId,
            string directoryId,
            string fileId,
            string blobId,
            string fileName,
            int fileSize,
            Dictionary<string, string> encryptedKeys,
            DocumentLanguage documentLanguage,
            ExtensionType fileExtension)
        {
            using var session = await client.StartSessionAsync();
            session.StartTransaction();

            try
            {
                // Find target directory 
                var directory = await directoryCollection.AsQueryable()
                    .Where(x =>
                        x.Id == directoryId &&
                        (x.OwnerId == requestingUserId ||
                         x.Permissions.Any(y =>
                             y.Permission.HasFlag(Permission.ReadWrite) &&
                             y.UserId == requestingUserId
                         )))
                    .Select(x => new
                    {
                        Owner = x.OwnerId,
                        Permissions = x.Permissions,
                        Path = x.PathIds
                    })
                    .FirstOrDefaultAsync();

                // Return if the user has no permissions or the directory does not exists
                if (directory == null)
                {
                    throw new ObjectNotResolvableException(
                        $"Directory {directoryId} could not be found or accessed by user {requestingUserId} during file insertion");

                }

                var newPath = directory.Path.ToList();
                newPath.Add(fileId);

                // Insert new file
                var file = new FileDto
                {
                    Id = fileId,
                    Language = documentLanguage,
                    Extension = fileExtension,
                    OwnerId = directory.Owner,
                    ObjectName = fileName,
                    ParentDirectoryId = directoryId,
                    PathIds = newPath.ToArray(),
                    RevisionIds = new[]
                    {
                        blobId
                    },
                    Permissions = directory.Permissions,
                    Tags = new ITag[0],
                };

                var insertFileTask = filesCollection.InsertOneAsync(session, file);


                // Update directory
                var updateFiles = Builders<DirectoryDto>.Update.Push(e => e.FileIds, fileId);
                var updateDirectoryTask =
                    directoryCollection.UpdateOneAsync(session, x => x.Id == directoryId, updateFiles);

                // Get user information
                var ownerId = directory.Owner;
                var userIds = new HashSet<string>(directory.Permissions.Select(x => x.UserId));

                // Ensure all keys are delivered
                if (userIds.Count != encryptedKeys.Count || !encryptedKeys.Keys.All(x => userIds.Contains(x)))
                {
                    throw new InvalidParameterException(
                        "Number of given keys does not match the number of required keys during file insertion");
                }

                var userInfo = await userCollection
                    .AsQueryable()
                    .Where(x => x.Id == ownerId)
                    .Select(x => new
                    {
                        UserId = x.Id,
                        QuickNumber = x.QuickNumber,
                    }).FirstOrDefaultAsync();

                // Update quick number
                var newQuickNumber = userInfo.QuickNumber + 1;
                var updateQuickNumber = Builders<UserDto>.Update.Set(x => x.QuickNumber, newQuickNumber);
                var updateUserTask = userCollection.UpdateOneAsync(session, x => x.Id == ownerId, updateQuickNumber);

                // Update revision
                var accessKeys = new List<AccessKeyDto>();
                foreach (var kv in encryptedKeys)
                {
                    var userId = kv.Key;
                    var encryptedKey = kv.Value;

                    accessKeys.Add(new AccessKeyDto
                    {
                        SymmetricEncryptedFileKey = encryptedKey,
                        IssuerId = requestingUserId,
                        UserId = userId
                    });
                }

                var revision = new FileRevisionDto
                {
                    Id = blobId,
                    File = fileId,
                    OwnerId = directory.Owner,
                    SizeInBytes = (uint)fileSize,
                    ChangeDate = DateTime.UtcNow,
                    QuickNumber = newQuickNumber,
                    AccessKeys = accessKeys.ToArray()
                };

                var insertRevisionTask = fileRevisionCollection.InsertOneAsync(session, revision);


                // Wait for completion
                await Task.WhenAll(insertRevisionTask, updateDirectoryTask, updateUserTask, insertFileTask);
                await session.CommitTransactionAsync();
                return newQuickNumber;
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
        public async Task<IFileMetadata> GetFileMetadata(string requestingUserId, string fileId, string revisionId)
        {
            try
            {
                var fileDto = await filesCollection
                    .AsQueryable()
                    .Where(x =>
                        x.Id == fileId &&
                        x.Permissions.Any(y =>
                            y.Permission.HasFlag(Permission.Read) &&
                            y.UserId == requestingUserId
                    ))
                    .FirstOrDefaultAsync();

                if (fileDto == null)
                {
                    throw new ObjectNotResolvableException(
                        $"File {fileId} could not be found or accessed by user {requestingUserId} during file metadata request");
                }

                fileDto = fileDto.RestrictPermissions(requestingUserId);

                if (revisionId == null)
                {
                    // Get all revisions
                    var fileRevisions = await fileRevisionCollection.Find(x => x.File == fileId).ToListAsync();
                    fileRevisions = fileRevisions.RestrictAccessKeys(requestingUserId);
                    return new File(fileDto, fileRevisions.ToArray());
                }

                // Get single revision
                var fileRevision = await fileRevisionCollection.Find(
                        x => x.File == fileId &&
                             x.Id == revisionId
                         ).FirstOrDefaultAsync();

                fileRevision = fileRevision.RestrictAccessKeys(requestingUserId);
                return new File(fileDto, new[] { fileRevision });
            }
            catch (ObjectNotResolvableException)
            {
                throw;
            }
            catch (MongoException e)
            {
                throw new DatabaseException("Database error during file inserting. See inner exception.", e);
            }
            catch (Exception e)
            {
                throw new Exception("Unknown error during file inserting. See inner exception.", e);
            }
        }
    }
}
