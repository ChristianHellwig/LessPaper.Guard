using System;
using System.Collections.Generic;
using System.Text;
using LessPaper.Shared.Enums;
using LessPaper.Shared.Helper;
using Microsoft.AspNetCore.Components.Forms;
using Xunit;

namespace LessPaper.GuardService.IntegrationTest
{
    public class DirectoryTest : MongoTestBase
    {
        protected string user1Directory1Name = "Dir_1";
        protected string user1Directory1Id = IdGenerator.NewId(IdType.Directory);
        protected string user2Directory2Name = "Dir_2";
        protected string user2Directory2Id = IdGenerator.NewId(IdType.Directory);


        [Fact]
        public async void DirectoryAdd()
        {
            Assert.True(await UserManager.InsertUser(User1Id, User1RootDirId, User1Email, User1HashedPassword, User1Salt));
            Assert.True(await DirectoryManager.InsertDirectory(
                User1Id, 
                User1RootDirId, 
                user1Directory1Name, 
                user1Directory1Id));

            var directory = await DirectoryManager.GetDirectoryMetadata(User1Id, user1Directory1Id, null);
            Assert.Equal(user1Directory1Id, directory.ObjectId);
            Assert.Equal(user1Directory1Name, directory.ObjectName);
            Assert.Empty(directory.DirectoryChilds);
            Assert.Empty(directory.FileChilds);

            Assert.Single(directory.Permissions);
            Assert.True(directory.Permissions[User1Id] ==
                (Permission.ReadWrite |
                Permission.Read |
                Permission.ReadPermissions |
                Permission.ReadWritePermissions)
            );

        }

    }
}
