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

namespace LessPaper.GuardService.Models.Database
{
    public class DbUserManager : IDbUserManager
    {
        private readonly IMongoClient client;
        private readonly IMongoCollection<MinimalUserInformationDto> userCollection;
        private readonly IMongoCollection<DirectoryDto> directoryCollection;

        public DbUserManager(IMongoClient client, IMongoCollection<MinimalUserInformationDto> userCollection, IMongoCollection<DirectoryDto> directoryCollection)
        {
            this.client = client;
            this.userCollection = userCollection;
            this.directoryCollection = directoryCollection;
        }

        /// <inheritdoc />
        public async Task<bool> InsertUser(string userId, string rootDirectoryId, string email, string hashedPassword, string salt)
        {
            if (IdGenerator.TypeFromId(userId, out var userIdType) && userIdType != IdType.User ||
                IdGenerator.TypeFromId(rootDirectoryId, out var rootDirectoryIdType) && rootDirectoryIdType != IdType.Directory)
            {
                return false;
            }
            
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
                        ObjectName = "__root_dir",
                        Directories = new List<MongoDBRef>(),
                        Files = new List<FileDto>(),
                        Permissions = new[]
                                {
                                    new DirectoryPermissionDto
                                    {
                                        User = new MongoDBRef("user", userId),
                                        Permission = Permission.Read | Permission.Write | Permission.ReadPermissions | Permission.WritePermissions
                                    }
                                },
                    };

                    var insertRootDirectoryTask = directoryCollection.InsertOneAsync(newRootDirectory);

                    var newUser = new MinimalUserInformationDto
                    {
                        Id = userId,
                        RootDirectory = new MongoDBRef("directories", newRootDirectory.Id),
                        Email = email,
                        PasswordHash = hashedPassword,
                        Salt = salt,
                        PublicKey = CryptoHelper.GenerateRsaKeyPair().PublicKey
                    };

                    var insertUserTask =  userCollection.InsertOneAsync(newUser);

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
        public async Task<bool> DeleteUser(string requestingUserId, string userId)
        {
            if (requestingUserId != userId ||
                 IdGenerator.TypeFromId(requestingUserId, out var requestingUserIdType) && requestingUserIdType != IdType.User ||
                 IdGenerator.TypeFromId(userId, out var userIdType) && userIdType != IdType.User)
            {
                return false;
            }

            var deleteDirectoriesTask = directoryCollection.DeleteManyAsync(directory => directory.Owner.Id.AsString == userId);
            var deleteUserTask = userCollection.DeleteOneAsync(user => user.Id == userId);

            await Task.WhenAll(deleteDirectoriesTask, deleteUserTask);

            return true;
        }

        /// <inheritdoc />
        public async Task<IMinimalUserInformation> GetBasicUserInformation(string requestingUserId, string userId)
        {
            if (requestingUserId != userId ||
                IdGenerator.TypeFromId(requestingUserId, out var requestingUserIdType) && requestingUserIdType != IdType.User ||
                IdGenerator.TypeFromId(userId, out var userIdType) && userIdType != IdType.User)
            {
                return null;
            }

            var userInformationDto = await userCollection.Find(user => user.Id == userId).FirstOrDefaultAsync();
            return new MinimalUserInformation(userInformationDto);
        }
    }
}
