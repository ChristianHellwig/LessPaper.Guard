using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using LessPaper.Guard.Database.MongoDb.Interfaces;
using LessPaper.Guard.Database.MongoDb.Models.Dtos;
using LessPaper.Guard.Database.MongoDb.Models.Implement;
using LessPaper.Shared.Enums;
using LessPaper.Shared.Interfaces.Database.Manager;
using LessPaper.Shared.Interfaces.General;
using LessPaper.Shared.Models.Exceptions;
using MongoDB.Driver;
using MongoDB.Driver.Linq;

namespace LessPaper.Guard.Database.MongoDb.Models
{
    public class DbUserManager : IDbUserManager
    {
        private readonly IMongoClient client;
        private readonly IMongoCollection<UserDto> userCollection;
        private readonly IMongoCollection<DirectoryDto> directoryCollection;
        private readonly IMongoCollection<FileDto> filesCollection;
        private readonly IMongoCollection<FileRevisionDto> fileRevisionCollection;

        public DbUserManager(IDatabaseManager dbManager)
        {
            client = dbManager.Client;
            directoryCollection = dbManager.DirectoryCollection;
            userCollection = dbManager.UserCollection;
            filesCollection = dbManager.FilesCollection;
            fileRevisionCollection = dbManager.FileRevisionCollection;
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
                        OwnerId = userId,
                        ObjectName = "__root_dir__",
                        DirectoryIds = new List<string>(),
                        FileIds = new List<string>(),
                        PathIds = new[] { rootDirectoryId },
                        Permissions = new[]
                        {
                            new BasicPermissionDto
                            {
                                UserId = userId,
                                Permission = Permission.Read | Permission.ReadWrite | Permission.ReadPermissions | Permission.ReadWritePermissions
                            }
                        },
                        
                    };
                    var insertRootDirectoryTask = directoryCollection.InsertOneAsync(session, newRootDirectory);

                    var newUser = new UserDto
                    {
                        Id = userId,
                        RootDirectory = newRootDirectory.Id,
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
                catch (MongoException e)
                {
                    await session.AbortTransactionAsync();
                    throw new DatabaseException("Database error during user creating. See inner exception.", e);
                }
                catch (Exception e)
                {
                    await session.AbortTransactionAsync();
                    throw new Exception("Unknown error during file inserting. See inner exception.", e);
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

            try
            {
                session.StartTransaction();

                var revisionIds = await fileRevisionCollection
                    .AsQueryable()
                    .Where(x => x.OwnerId == userId)
                    .Select(x => x.Id)
                    .ToListAsync();

                var deleteRevisionsTask = fileRevisionCollection.DeleteManyAsync(session,
                    directory => directory.OwnerId == userId);

                var deleteFilesTask = filesCollection.DeleteManyAsync(session,
                    directory => directory.OwnerId == userId);

                var deleteDirectoriesTask = directoryCollection.DeleteManyAsync(session,
                    directory => directory.OwnerId == userId);

                var deleteUserTask = userCollection.DeleteOneAsync(session, user => user.Id == userId);

                await Task.WhenAll(deleteDirectoriesTask, deleteRevisionsTask, deleteFilesTask, deleteUserTask);
                await session.CommitTransactionAsync();

                return revisionIds.ToArray();
            }
            catch (MongoException e)
            {
                await session.AbortTransactionAsync();
                throw new DatabaseException("Database error during user delete. See inner exception.", e);
            }
            catch (Exception e)
            {
                await session.AbortTransactionAsync();
                throw new Exception("Unknown error during user delete. See inner exception.", e);
            }
        }

        /// <inheritdoc />
        public async Task<IMinimalUserInformation> GetBasicUserInformation(string requestingUserId, string userId)
        {
            if (requestingUserId != userId)
            {
                throw new ObjectNotResolvableException(
                    $"User {userId} could not be accessed by user {requestingUserId} during user information request");
            }

            var userInformationDto = await userCollection.Find(user => user.Id == userId).FirstOrDefaultAsync();

            if (userInformationDto == null)
            {
                throw new ObjectNotResolvableException(
                    $"User {userId} could not be found by user {requestingUserId} during user information request");
            }
            
            return new MinimalUserInformation(userInformationDto);
        }
    }
}
