using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Schema;
using LessPaper.GuardService.Models.Database.Dtos;
using LessPaper.GuardService.Models.Database.Helper;
using LessPaper.GuardService.Models.Database.Implement;
using LessPaper.Shared.Enums;
using LessPaper.Shared.Helper;
using LessPaper.Shared.Interfaces.Database.Manager;
using LessPaper.Shared.Interfaces.General;
using LessPaper.Shared.Interfaces.GuardApi.Response;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Driver;
using MongoDB.Driver.Linq;

namespace LessPaper.GuardService.Models.Database
{
    public class DbFileManager : IDbFileManager
    {
        private readonly IMongoClient client;
        private readonly IMongoTables tables;
        private readonly IMongoCollection<DirectoryDto> directoryCollection;
        private readonly IMongoCollection<UserDto> userCollection;
        private readonly IMongoCollection<FileDto> filesCollection;
        private readonly IMongoCollection<FileRevisionDto> fileRevisionCollection;

        public DbFileManager(
            IMongoTables tables,
            IMongoClient client,
            IMongoCollection<DirectoryDto> directoryCollection,
            IMongoCollection<UserDto> userCollection,
            IMongoCollection<FileDto> filesCollection,
            IMongoCollection<FileRevisionDto> fileRevisionCollection)
        {
            this.client = client;
            this.tables = tables;
            this.directoryCollection = directoryCollection;
            this.userCollection = userCollection;
            this.filesCollection = filesCollection;
            this.fileRevisionCollection = fileRevisionCollection;
        }


        private class TempRevisionView
        {
            public List<MongoDBRef> Revisions { get; set; }

            [BsonId]
            public string Id { get; set; }
        }

        /// <inheritdoc />
        public async Task<string[]> Delete(string requestingUserId, string fileId)
        {
            using var session = await client.StartSessionAsync();
            session.StartTransaction();

            

            try
            {

                var directoryUpdate = Builders<DirectoryDto>.Update.Pull(x => x.Files, new MongoDBRef(tables.FilesTable, fileId));

                var updatedDirectoryTask = directoryCollection.UpdateOneAsync(session,
                    x => x.Permissions.Any(y =>
                        y.Permission.HasFlag(Permission.ReadWrite) &&
                        y.User == new MongoDBRef(tables.UserTable, requestingUserId)
                ), directoryUpdate);

                var revisionsTask = fileRevisionCollection.DeleteManyAsync(session, x => x.File == new MongoDBRef(tables.FilesTable, fileId));

                var deadRevisions = await filesCollection.FindOneAndDeleteAsync(
                    session,
                    x => x.Id == fileId, new FindOneAndDeleteOptions<FileDto, TempRevisionView>
                    {
                        Projection = Builders<FileDto>.Projection.Include(x => x.Revisions)
                    });
                
                await Task.WhenAll(updatedDirectoryTask, revisionsTask);
                await session.CommitTransactionAsync();

                return deadRevisions.Revisions.Select(x => x.Id.AsString).ToArray();
            }
            catch (Exception e)
            {
                Console.WriteLine("Error writing to MongoDB: " + e.Message);
                await session.AbortTransactionAsync();
                return null;
            }
        }

        /// <inheritdoc />
        public async Task<IPermissionResponse[]> GetPermissions(string requestingUserId, string userId, string[] objectIds)
        {
            var filePermissions = await filesCollection.AsQueryable()
                .Where(x =>
                    objectIds.Contains(x.Id) &&
                    x.Permissions.Any(y =>
                        (y.Permission.HasFlag(Permission.Read) ||
                         y.Permission.HasFlag(Permission.ReadPermissions)) &&
                        y.User == new MongoDBRef(tables.UserTable, requestingUserId)
                    ))
                .Select(x => new
                {
                    Id = x.Id,
                    Permissions = x.Permissions.Select(y => new BasicPermissionDto
                    {
                        Permission = y.Permission,
                        User = y.User,
                    }).ToArray()
                })
                .ToListAsync();

            // Restrict response to relevant and viewable permissions
            var responseObj = new List<IPermissionResponse>(filePermissions.Count);
            if (requestingUserId == userId)
            {
                // Require at least a read flag
                responseObj.AddRange((from directoryPermission in filePermissions
                                      let permissionEntry = directoryPermission.Permissions
                                          .RestrictPermissions(requestingUserId)
                                          .FirstOrDefault()
                                      where permissionEntry != null
                                      select new PermissionResponse(directoryPermission.Id, permissionEntry.Permission)).Cast<IPermissionResponse>());
            }
            else
            {
                responseObj.AddRange(from directoryPermission in filePermissions
                                     let permissionEntry = directoryPermission.Permissions
                                         .RestrictPermissions(requestingUserId)
                                         .FirstOrDefault(x => x.User.Id.AsString == userId)
                                     where permissionEntry != null
                                     select new PermissionResponse(directoryPermission.Id, permissionEntry.Permission));
            }

            return responseObj.ToArray();
        }

        /// <inheritdoc />
        public async Task<bool> Rename(string requestingUserId, string objectId, string newName)
        {
            var update = Builders<FileDto>.Update.Set(x => x.ObjectName, newName);

            var updateResult = await filesCollection.UpdateOneAsync(x => 
                x.Id == objectId &&
                x.Permissions.Any(y =>
                    y.Permission.HasFlag(Permission.ReadWrite) &&
                    y.User == new MongoDBRef(tables.UserTable, requestingUserId)
                ), update);

            return updateResult.ModifiedCount == 1;
        }

        /// <inheritdoc />
        public async Task<bool> Move(string requestingUserId, string objectId, string targetDirectoryId)
        {

            using var session = await client.StartSessionAsync();
            session.StartTransaction();

            var cancellationTokenSource = new CancellationTokenSource();

            try
            {
                var popUpdate = Builders<DirectoryDto>.Update.PullFilter(x => x.Files, x => x.Id.AsString == objectId);

                // Remove file from directory
                var removeFileFromOldDirectoryTask = directoryCollection.UpdateOneAsync(session, x =>
                    x.Id != targetDirectoryId &&
                    x.Files.Contains(new MongoDBRef(tables.FilesTable, objectId)) &&
                    x.Permissions.Any(y =>
                        y.Permission.HasFlag(Permission.ReadWrite) &&
                        y.User == new MongoDBRef(tables.UserTable, requestingUserId)
                    ), popUpdate, cancellationToken: cancellationTokenSource.Token);

                // Add file to directory
                var addUpdate =
                    Builders<DirectoryDto>.Update.Push(x => x.Files, new MongoDBRef(tables.FilesTable, objectId));

                var addFileToNewDirectory = await directoryCollection.FindOneAndUpdateAsync(session, x =>
                    x.Id == targetDirectoryId &&
                    x.Permissions.Any(y =>
                        y.Permission.HasFlag(Permission.ReadWrite) &&
                        y.User == new MongoDBRef(tables.UserTable, requestingUserId)
                    ), addUpdate, cancellationToken: cancellationTokenSource.Token);

                // Cancel if the new directory could not be resolved
                if (addFileToNewDirectory == null)
                {
                    cancellationTokenSource.Cancel();
                    await session.AbortTransactionAsync();
                    return false;
                }


                // Update parent directory of file
                var fileParentDirectoryUpdate = Builders<FileDto>.Update.Set(x => x.ParentDirectory,
                    new MongoDBRef(tables.DirectoryTable, targetDirectoryId));

                var filePermissionsUpdate =
                    Builders<FileDto>.Update.Set(x => x.Permissions, addFileToNewDirectory.Permissions);

                var fileUpdate = Builders<FileDto>.Update.Combine(fileParentDirectoryUpdate, filePermissionsUpdate);
                var updateFile = await filesCollection.FindOneAndUpdateAsync(session, x => x.Id == objectId, fileUpdate);


                //TODO Revisions need the key if a file is moved in a directory with new people


                // Wait for tasks
                await Task.WhenAll(removeFileFromOldDirectoryTask);

                // Check if all tasks modified exactly one file
                if (removeFileFromOldDirectoryTask.Result.ModifiedCount != 1 ||
                    updateFile == null)
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
                        x.Permissions.Any(y =>
                            y.Permission.HasFlag(Permission.ReadWrite) &&
                            y.User == new MongoDBRef(tables.UserTable, requestingUserId)
                        ))
                    .Select(x => new
                    {
                        Owner = x.Owner,
                        Permissions = x.Permissions
                    })
                    .FirstOrDefaultAsync();

                // Return if the user has no permissions or the directory does not exists
                if (directory == null)
                    return 0;

                // Insert new file
                var file = new FileDto
                {
                    Id = fileId,
                    Language = documentLanguage,
                    Extension = fileExtension,
                    Owner = directory.Owner,
                    ObjectName = fileName,
                    ParentDirectory = new MongoDBRef(tables.DirectoryTable, directoryId),
                    Revisions = new[]
                    {
                        new MongoDBRef(tables.RevisionTable, blobId)
                    },
                    Permissions = directory.Permissions,
                    Tags = new ITag[0],
                };

                var insertFileTask = filesCollection.InsertOneAsync(session, file);
                var fileRef = new MongoDBRef(tables.FilesTable, fileId);

                // Update directory
                var updateFiles = Builders<DirectoryDto>.Update.Push(e => e.Files, fileRef);
                var updateDirectoryTask = directoryCollection.UpdateOneAsync(session, x => x.Id == directoryId, updateFiles);

                // Get user information
                var ownerId = directory.Owner.Id.AsString;
                var userIds = directory.Permissions.Select(x => x.User.Id.AsString).ToHashSet();

                // Ensure all keys are delivered
                if (userIds.Count != encryptedKeys.Count || !encryptedKeys.Keys.All(x => userIds.Contains(x)))
                    throw new Exception("Invalid number of keys");

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
                foreach (var (userId, encryptedKey) in encryptedKeys)
                {
                    accessKeys.Add(new AccessKeyDto
                    {
                        SymmetricEncryptedFileKey = encryptedKey,
                        Issuer = new MongoDBRef(tables.UserTable, requestingUserId),
                        User = new MongoDBRef(tables.UserTable, userId)
                    });
                }

                var revision = new FileRevisionDto
                {
                    Id = blobId,
                    File = fileRef,
                    Owner = directory.Owner,
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
            catch (Exception e)
            {
                Console.WriteLine("Error writing to MongoDB: " + e.Message);
                await session.AbortTransactionAsync();
                return 0;
            }
        }

        /// <inheritdoc />
        public async Task<IFileMetadata> GetFileMetadata(string requestingUserId, string fileId, string revisionId)
        {
            var fileDto = await filesCollection
                .AsQueryable()
                .Where(x =>
                    x.Id == fileId &&
                    x.Permissions.Any(y =>
                        y.Permission.HasFlag(Permission.Read) &&
                        y.User == new MongoDBRef(tables.UserTable, requestingUserId)
                ))
                .FirstOrDefaultAsync();

            if (fileDto == null)
                return null;

            fileDto = fileDto.RestrictPermissions(requestingUserId);


            if (revisionId == null)
            {
                // Get all revisions
                var fileRevisions = await fileRevisionCollection.Find(x => x.File == new MongoDBRef(tables.FilesTable, fileId)).ToListAsync();
                fileRevisions = fileRevisions.RestrictAccessKeys(requestingUserId);
                return new File(fileDto, fileRevisions.ToArray());
            }

            // Get single revision
            var fileRevision = await fileRevisionCollection.Find(
                x => x.File == new MongoDBRef(tables.FilesTable, fileId) && x.Id == revisionId).FirstOrDefaultAsync();

            fileRevision = fileRevision.RestrictAccessKeys(requestingUserId);
            return new File(fileDto, new FileRevisionDto[] { fileRevision });
        }
    }
}
