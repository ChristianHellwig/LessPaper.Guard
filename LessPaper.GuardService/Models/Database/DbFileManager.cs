using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using LessPaper.GuardService.Models.Database.Dtos;
using LessPaper.GuardService.Models.Database.Implement;
using LessPaper.Shared.Enums;
using LessPaper.Shared.Helper;
using LessPaper.Shared.Interfaces.Database.Manager;
using LessPaper.Shared.Interfaces.General;
using LessPaper.Shared.Interfaces.GuardApi.Response;
using MongoDB.Driver;
using MongoDB.Driver.Linq;

namespace LessPaper.GuardService.Models.Database
{
    public class DbFileManager : IDbFileManager
    {
        private readonly IMongoClient client;
        private readonly IMongoCollection<DirectoryDto> directoryCollection;
        private readonly IMongoCollection<MinimalUserInformationDto> userCollection;

        public DbFileManager(IMongoClient client, IMongoCollection<DirectoryDto> directoryCollection,
            IMongoCollection<MinimalUserInformationDto> userCollection)
        {
            this.client = client;
            this.directoryCollection = directoryCollection;
            this.userCollection = userCollection;
        }

        /// <inheritdoc />
        public async Task<bool> DeleteFile(string requestingUserId, string fileId)
        {
            using var session = await client.StartSessionAsync();
            session.StartTransaction();

            try
            {
                var update = Builders<DirectoryDto>.Update.PullFilter(p => p.Files,
                    f => f.Id == fileId);

                var result = await directoryCollection.FindOneAndUpdateAsync(
                    x => x.Permissions.Any(y =>
                        y.Permission.HasFlag(Permission.Write) &&
                        y.User == new MongoDBRef("user", requestingUserId)
                ), update);


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
        public async Task<IPermissionResponse> GetFilePermissions(string requestingUserId, string userId, string[] objectIds)
        {

            var filePermissions = await directoryCollection.AsQueryable()
                .SelectMany(x => x.Files)
                .Where(x =>
                    objectIds.Contains(x.Id) &&
                    x.Permissions.Any(y =>
                        y.Permission.HasFlag(Permission.ReadPermissions) &&
                        y.User == new MongoDBRef("user", requestingUserId)
                    ))
                .Select(x => new
                {
                    Id = x.Id,
                    Permissions = x.Permissions
                })
                .ToListAsync();


            return null;

        }

        /// <inheritdoc />
        public async Task<int> InsertFile(
            string requestingUserId,
            string directoryId,
            string fileId,
            string blobId,
            string fileName,
            int fileSize,
            string encryptedKey,
            DocumentLanguage documentLanguage,
            ExtensionType fileExtension)
        {
            using var session = await client.StartSessionAsync();
            session.StartTransaction();

            try
            {
                var directory = await directoryCollection.AsQueryable()
                    .Where(x =>
                        x.Id == directoryId &&
                        x.Permissions.Any(y =>
                            y.Permission.HasFlag(Permission.Write) &&
                            y.User == new MongoDBRef("user", requestingUserId)
                        ))
                    .Select(x => new
                    {
                        Owner = x.Owner,
                        Permissions = x.Permissions
                    })
                    .FirstOrDefaultAsync();

                if (directory == null)
                    return -1;

                var ownerId = directory.Owner.Id.AsString;

                var userIds = directory.Permissions.Select(x => x.User.Id.AsString).ToArray();
                var publicKeys = await userCollection
                    .AsQueryable()
                    .Where(x => userIds.Contains(x.Id))
                    .Select(x => new
                    {
                        UserId = x.Id,
                        PublicKey = x.PublicKey,
                        QuickNumber = x.QuickNumber,
                    }).ToListAsync();


                var publicKeyDict = publicKeys.ToDictionary(x => x.UserId, x => x);
                var newQuickNumber = publicKeyDict[ownerId].QuickNumber + 1;

                var file = new FileDto
                {
                    Id = fileId,
                    Language = documentLanguage,
                    Extension = fileExtension,
                    Owner = directory.Owner,
                    ObjectName = fileName,
                    ParentDirectoryIds = new[] { directoryId },
                    Revisions = new[]
                    {
                        new FileRevisionDto
                        {
                            SizeInBytes = (uint)fileSize,
                            ChangeDate = DateTime.UtcNow,
                            RevisionNumber = 0,
                            BlobId = blobId
                        }
                    },
                    Permissions = directory.Permissions
                        .Where(x => publicKeyDict.ContainsKey(x.User.Id.AsString))
                        .Select(x => new FilePermissionDto
                        {
                            User = x.User,
                            Permission = x.Permission,
                            EncryptedKey = CryptoHelper.RsaEncrypt(publicKeyDict[x.User.Id.AsString].PublicKey, encryptedKey)
                        }).ToArray(),
                    Tags = new ITag[0],
                    QuickNumber = newQuickNumber
                };

                var updateFiles = Builders<DirectoryDto>.Update.Push(e => e.Files, file);
                var updateDirectoryTask = directoryCollection.FindOneAndUpdateAsync(x =>
                   x.Id == directoryId &&
                   x.Permissions.Any(y =>
                       y.Permission.HasFlag(Permission.Write) &&
                       y.User == new MongoDBRef("user", requestingUserId)
                   ), updateFiles);

                var updateQuickNumber = Builders<MinimalUserInformationDto>.Update.Set(x => x.QuickNumber, newQuickNumber);
                var updateUserTask = userCollection.FindOneAndUpdateAsync(x => x.Id == ownerId, updateQuickNumber);

                await Task.WhenAll(updateDirectoryTask, updateUserTask);
                await session.CommitTransactionAsync();
                return 0;
            }
            catch (Exception e)
            {
                Console.WriteLine("Error writing to MongoDB: " + e.Message);
                await session.AbortTransactionAsync();
                return 0;
            }
        }

        /// <inheritdoc />
        public async Task<IFileMetadata> GetFileMetadata(string requestingUserId, string objectId, uint? revisionNumber)
        {
            var fileDto = await directoryCollection
                .AsQueryable()
                .SelectMany(x => x.Files)
                .Where(x =>
                    x.Id == objectId &&
                    x.Permissions.Any(y =>
                        y.Permission.HasFlag(Permission.Read) &&
                        y.User == new MongoDBRef("user", requestingUserId)
                ))
                .FirstOrDefaultAsync();

            if (fileDto == null)
                return null;

            if (!fileDto.Permissions.Any(x =>
                x.User.Id.AsString == requestingUserId && x.Permission.HasFlag(Permission.ReadPermissions)))
            {
                fileDto.Permissions = fileDto.Permissions.Where(x => x.User.Id.AsString == requestingUserId).ToArray();
            }

            if (revisionNumber == null)
                return new File(fileDto);
            
            var revision = fileDto.Revisions.FirstOrDefault(x => x.RevisionNumber == revisionNumber.Value);
            if (revision == null)
                return null;

            fileDto.Revisions = new[] { revision };
            
            return new File(fileDto);
        }
    }
}
