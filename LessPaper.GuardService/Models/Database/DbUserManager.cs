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
        private readonly IMongoTables tables;
        private readonly IMongoClient client;
        private readonly IMongoCollection<UserDto> userCollection;
        private readonly IMongoCollection<DirectoryDto> directoryCollection;
        private readonly IMongoCollection<FileDto> filesCollection;
        private readonly IMongoCollection<FileRevisionDto> fileRevisionCollection;

        public DbUserManager(
            IMongoTables tables, 
            IMongoClient client,
            IMongoCollection<UserDto> userCollection,
            IMongoCollection<DirectoryDto> directoryCollection, 
            IMongoCollection<FileDto> filesCollection, 
            IMongoCollection<FileRevisionDto> fileRevisionCollection)
        {
            this.tables = tables;
            this.client = client;
            this.userCollection = userCollection;
            this.directoryCollection = directoryCollection;
            this.filesCollection = filesCollection;
            this.fileRevisionCollection = fileRevisionCollection;
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
                        Owner = new MongoDBRef(tables.UserTable, userId),
                        ObjectName = "__root_dir__",
                        Directories = new List<MongoDBRef>(),
                        Files = new List<MongoDBRef>(),
                        Path = new[] { rootDirectoryId },
                        Permissions = new[]
                        {
                            new BasicPermissionDto
                            {
                                User = new MongoDBRef(tables.UserTable, userId),
                                Permission = Permission.Read | Permission.ReadWrite | Permission.ReadPermissions | Permission.ReadWritePermissions
                            }
                        },
                        
                    };

                    var insertRootDirectoryTask = directoryCollection.InsertOneAsync(session, newRootDirectory);

                    var newUser = new UserDto
                    {
                        Id = userId,
                        RootDirectory = new MongoDBRef(tables.DirectoryTable, newRootDirectory.Id),
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
                var deleteRevisionsTask = fileRevisionCollection.DeleteManyAsync(session,
                    directory => directory.Owner.Id.AsString == userId);

                //var revisionIdsx = await fileRevisionCollection.Find(x => x.QuickNumber == 0).ToListAsync();

                var mRef = new MongoDBRef(tables.UserTable, userId);
                var revisionIds = await fileRevisionCollection
                    .AsQueryable()
                    .Where(x => x.Owner == mRef)
                    .Select(x => x.Id)
                    .ToListAsync();
                
                var deleteFilesTask = filesCollection.DeleteManyAsync(session,
                    directory => directory.Owner.Id.AsString == userId);

                var deleteDirectoriesTask = directoryCollection.DeleteManyAsync(session,
                    directory => directory.Owner.Id.AsString == userId);

                var deleteUserTask = userCollection.DeleteOneAsync(session, user => user.Id == userId);

                await Task.WhenAll(deleteDirectoriesTask, deleteRevisionsTask, deleteFilesTask, deleteUserTask);
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
        public async Task<IMinimalUserInformation> GetBasicUserInformation(string requestingUserId, string userId)
        {
            if (requestingUserId != userId)
                return null;

            var userInformationDto = await userCollection.Find(user => user.Id == userId).FirstOrDefaultAsync();
            return userInformationDto == null ? null : new MinimalUserInformation(userInformationDto);
        }
    }
}
