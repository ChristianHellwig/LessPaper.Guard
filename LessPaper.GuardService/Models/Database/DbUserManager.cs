using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using LessPaper.GuardService.Models.Database.Dtos;
using LessPaper.GuardService.Models.Database.Implement;
using LessPaper.Shared.Enums;
using LessPaper.Shared.Helper;
using LessPaper.Shared.Interfaces.Database.Manager;
using LessPaper.Shared.Interfaces.General;
using MongoDB.Driver;
using MongoDB.Driver.Linq;

namespace LessPaper.GuardService.Models.Database
{
    public class DbUserManager : IDbUserManager
    {
        private readonly IMongoClient client;
        private readonly IMongoCollection<UserDto> userCollection;
        private readonly IMongoCollection<DirectoryDto> directoryCollection;

        public DbUserManager(IMongoClient client, IMongoCollection<UserDto> userCollection, IMongoCollection<DirectoryDto> directoryCollection)
        {
            this.client = client;
            this.userCollection = userCollection;
            this.directoryCollection = directoryCollection;
        }

        /// <inheritdoc />
        public async Task<bool> InsertUser(string userId, string rootDirectoryId, string email, string hashedPassword, string salt,
            string publicKey, string encryptedPrivateKey)
        {
            using (var session = await client.StartSessionAsync())
            {
                // Begin transaction
                session.StartTransaction();


                try
                {
                    var newRootDirectory = new DirectoryDto
                    {
                        Id = rootDirectoryId,
                        IsRootDirectory = true,
                        Owner = new MongoDBRef("user", userId),
                        ObjectName = "__root_dir__",
                        Directories = new List<MongoDBRef>(),
                        Files = new List<FileDto>(),
                        Path = new[] { rootDirectoryId },
                        Permissions = new[]
                        {
                            new BasicPermissionDto
                            {
                                User = new MongoDBRef("user", userId),
                                Permission = Permission.Read | Permission.ReadWrite | Permission.ReadPermissions | Permission.ReadWritePermissions
                            }
                        },
                        
                    };

                    var insertRootDirectoryTask = directoryCollection.InsertOneAsync(session, newRootDirectory);

                    var newUser = new UserDto
                    {
                        Id = userId,
                        RootDirectory = new MongoDBRef("directories", newRootDirectory.Id),
                        Email = email,
                        PasswordHash = hashedPassword,
                        Salt = salt,
                        PublicKey = publicKey,
                        EncryptedPrivateKey = encryptedPrivateKey
                    };

                    var insertUserTask = userCollection.InsertOneAsync(session, newUser);

                    await Task.WhenAll(insertRootDirectoryTask, insertUserTask);
                    await session.CommitTransactionAsync();
                }
                catch (Exception e)
                {
                    Console.WriteLine("Error writing to MongoDB: " + e.Message);
                    await session.AbortTransactionAsync();
                    return false;
                }
            }

            return true;

        }

        /// <inheritdoc />
        public async Task<string[]> DeleteUser(string requestingUserId, string userId)
        {
            if (requestingUserId != userId)
                return null;

            using var session = await client.StartSessionAsync();
            session.StartTransaction();

            try
            {
                var fileBlobs = await directoryCollection.AsQueryable()
                    .Where(x => x.Owner.Id.AsString == userId)
                    .SelectMany(x => x.Files)
                    .SelectMany(x => x.Revisions)
                    .Select(x => x.BlobId)
                    .ToListAsync();
                
                var deleteDirectoriesTask = directoryCollection.DeleteManyAsync(session,
                    directory => directory.Owner == new MongoDBRef("user", userId));
                var deleteUserTask = userCollection.DeleteOneAsync(session,user => user.Id == userId);

                await Task.WhenAll(deleteDirectoriesTask, deleteUserTask);
                await session.CommitTransactionAsync();

                return fileBlobs.ToArray();
            }
            catch (Exception e)
            {
                Console.WriteLine("Error writing to MongoDB: " + e.Message);
                await session.AbortTransactionAsync();
                return null;
            }
        }

        /// <inheritdoc />
        public async Task<IMinimalUserInformation> GetBasicUserInformation(string requestingUserId, string userId)
        {
            if (requestingUserId != userId)
                return null;

            var userInformationDto = await userCollection.Find(user => user.Id == userId).FirstOrDefaultAsync();
            return userInformationDto == null ? null : new MinimalUserInformation(userInformationDto);
        }
    }
}
