using System;
using System.Diagnostics;
using LessPaper.GuardService.Models.Database;
using LessPaper.Shared.Enums;
using LessPaper.Shared.Helper;
using LessPaper.Shared.Interfaces.Database.Manager;
using MongoDB.Bson;
using MongoDB.Driver;
using MongoDB.Driver.Core.Events;
using Xunit;

namespace LessPaper.GuardService.IntegrationTest
{
    public class UserTest : MongoTestBase
    {
        [Fact]
        public async void UserDuplicateEmail()
        {
            Assert.True(await UserManager.InsertUser(User1Id, User1RootDirId, User1Email, User1HashedPassword, User1Salt));
            Assert.False(await UserManager.InsertUser(User2Id, User2RootDirId, User1Email, User2HashedPassword, User2Salt));
        }

        [Fact]
        public async void UserDuplicateUserId()
        {
            Assert.True(await UserManager.InsertUser(User1Id, User1RootDirId, User1Email, User1HashedPassword, User1Salt));
            Assert.False(await UserManager.InsertUser(User1Id, User2RootDirId, User2Email, User2HashedPassword, User2Salt));
        }

        [Fact]
        public async void UserDuplicateRootDirectoryId()
        {
            Assert.True(await UserManager.InsertUser(User1Id, User1RootDirId, User1Email, User1HashedPassword, User1Salt));
            Assert.False(await UserManager.InsertUser(User2Id, User1RootDirId, User2Email, User2HashedPassword, User2Salt));
            Assert.Null(await UserManager.GetBasicUserInformation(User2Id, User2Id));
        }


        [Fact]
        public async void UserInvalidIds()
        {
            Assert.False(await UserManager.InsertUser("1234567", User1RootDirId, User2Email, User2HashedPassword, User2Salt));
            Assert.False(await UserManager.InsertUser(User1Id, "1234567", User2Email, User2HashedPassword, User2Salt));
        }


        [Fact]
        public async void UserDelete()
        {
            Assert.True(await UserManager.InsertUser(User1Id, User1RootDirId, User1Email, User1HashedPassword, User1Salt));
            Assert.True(await UserManager.DeleteUser(User1Id, User1Id));
        }


        [Fact]
        public async void UserDeleteByOtherUser()
        {
            Assert.True(await UserManager.InsertUser(User1Id, User1RootDirId, User1Email, User1HashedPassword, User1Salt));
            Assert.True(await UserManager.InsertUser(User2Id, User2RootDirId, User2Email, User2HashedPassword, User2Salt));
            Assert.False(await UserManager.DeleteUser(User1Id, User2Id));
        }


        [Fact]
        public async void UserDeleteNonExisting()
        {
            Assert.False(await UserManager.DeleteUser(User1Id, User2Id));
        }


        [Fact]
        public async void UserGetInfo()
        {
            Assert.True(await UserManager.InsertUser(User1Id, User1RootDirId, User1Email, User1HashedPassword, User1Salt));
            Assert.True(await UserManager.InsertUser(User2Id, User2RootDirId, User2Email, User2HashedPassword, User2Salt));

            var user = await UserManager.GetBasicUserInformation(User1Id, User1Id);
            Assert.Equal(User1Email, user.Email);
            Assert.Equal(User1HashedPassword, user.PasswordHash);
            Assert.Equal(User1RootDirId, user.RootDirectoryId);
            Assert.Equal(User1Salt, user.Salt);
        }

        [Fact]
        public async void UserGetInfoFromOtherUser()
        {
            Assert.True(await UserManager.InsertUser(User1Id, User1RootDirId, User1Email, User1HashedPassword, User1Salt));
            Assert.True(await UserManager.InsertUser(User2Id, User2RootDirId, User2Email, User2HashedPassword, User2Salt));
            
            var user = await UserManager.GetBasicUserInformation(User1Id, User2Id);
            Assert.Null(user);
        }
    }
}
