using System;
using System.Collections.Generic;
using System.Linq;
using System.Security;
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
        public async void DirectoryShare()
        {
            var idRoot = IdGenerator.NewId(IdType.Directory);
            Assert.True(await UserManager.InsertUser(User1Id, idRoot, User1Email, User1HashedPassword, User1Salt, User1Keys.PublicKey, User1Keys.PrivateKey));

            var idA = IdGenerator.NewId(IdType.Directory);
            var idB = IdGenerator.NewId(IdType.Directory);
            var idC = IdGenerator.NewId(IdType.Directory);

            Assert.True(await DirectoryManager.InsertDirectory(
                User1Id,
                idRoot,
                "A",
                idA));

            Assert.True(await DirectoryManager.InsertDirectory(
                User1Id,
                idRoot,
                "B",
                idB));

            Assert.True(await DirectoryManager.InsertDirectory(
                User1Id,
                idA,
                "C",
                idC));

            var quickNumberA = await FileManager.InsertFile(User1Id, idA, IdGenerator.NewId(IdType.File), IdGenerator.NewId(IdType.FileBlob),
                "A_File", 10, "MyEncryptedKey", DocumentLanguage.German, ExtensionType.Png);


            for (uint i = 1; i < 100; i++)
            {
               var quickNumber = await FileManager.InsertFile(User1Id, idC, IdGenerator.NewId(IdType.File), IdGenerator.NewId(IdType.FileBlob),
                    "C_File" + i, 10, "MyEncryptedKey", DocumentLanguage.German, ExtensionType.Png);

                Assert.Equal(i + 1, quickNumber);
            }

            var test = await DirectoryManager.PrepareShare(User1Id, idC, new[] {User1Email});

        }

        [Fact]
        public async void DirectoryMove()
        {
            var idRoot = IdGenerator.NewId(IdType.Directory);

            Assert.True(await UserManager.InsertUser(User1Id, idRoot, User1Email, User1HashedPassword, User1Salt, User1Keys.PublicKey, User1Keys.PrivateKey));

            var idA = IdGenerator.NewId(IdType.Directory);
            var idB = IdGenerator.NewId(IdType.Directory);
            var idC = IdGenerator.NewId(IdType.Directory);

            // Root
            // | \
            // A  B
            // | 
            // C

            Assert.True(await DirectoryManager.InsertDirectory(
                User1Id,
                idRoot,
                "A",
                idA));


            Assert.True(await DirectoryManager.InsertDirectory(
                User1Id,
                idRoot,
                "B",
                idB));

            Assert.True(await DirectoryManager.InsertDirectory(
                User1Id,
                idA,
                "C",
                idC));


            var rootDirectory = await DirectoryManager.GetDirectoryMetadata(User1Id, idRoot, null);
            Assert.Equal(2, rootDirectory.DirectoryChilds.Length);
            Assert.Contains(rootDirectory.DirectoryChilds, x => x.ObjectId == idA);
            Assert.Contains(rootDirectory.DirectoryChilds, x => x.ObjectId == idB);

            var aDirectory = await DirectoryManager.GetDirectoryMetadata(User1Id, idA, null);
            Assert.Single(aDirectory.DirectoryChilds);
            Assert.Contains(aDirectory.DirectoryChilds, x => x.ObjectId == idC);

            var bDirectory = await DirectoryManager.GetDirectoryMetadata(User1Id, idB, null);
            Assert.Empty(bDirectory.DirectoryChilds);

            var cDirectory = await DirectoryManager.GetDirectoryMetadata(User1Id, idC, null);
            Assert.Empty(cDirectory.DirectoryChilds);

            var st = DateTime.UtcNow;
            await DirectoryManager.Move(User1Id, idA, idB);
            var dur = DateTime.UtcNow - st;

            // Root
            // |
            // B 
            // |  
            // A
            // |
            // C

            rootDirectory = await DirectoryManager.GetDirectoryMetadata(User1Id, idRoot, null);
            Assert.Single(rootDirectory.DirectoryChilds);
            Assert.Contains(rootDirectory.DirectoryChilds, x => x.ObjectId == idB);

            aDirectory = await DirectoryManager.GetDirectoryMetadata(User1Id, idA, null);
            Assert.Single(aDirectory.DirectoryChilds);
            Assert.Contains(aDirectory.DirectoryChilds, x => x.ObjectId == idC);

            bDirectory = await DirectoryManager.GetDirectoryMetadata(User1Id, idB, null);
            Assert.Single(bDirectory.DirectoryChilds);
            Assert.Contains(bDirectory.DirectoryChilds, x => x.ObjectId == idA);

            cDirectory = await DirectoryManager.GetDirectoryMetadata(User1Id, idC, null);
            Assert.Empty(cDirectory.DirectoryChilds);
        }

        [Fact]
        public async void DirectoryInsert_SameName()
        {
            Assert.True(await UserManager.InsertUser(User1Id, User1RootDirId, User1Email, User1HashedPassword, User1Salt, User1Keys.PublicKey, User1Keys.PrivateKey));
            Assert.True(await DirectoryManager.InsertDirectory(
                User1Id,
                User1RootDirId,
                user1Directory1Name,
                user1Directory1Id));

            Assert.False(await DirectoryManager.InsertDirectory(
                User1Id,
                User1RootDirId,
                user1Directory1Name,
                user2Directory2Id));
        }



        [Fact]
        public async void DirectoryInsert_Rename()
        {
            Assert.True(await UserManager.InsertUser(User1Id, User1RootDirId, User1Email, User1HashedPassword, User1Salt, User1Keys.PublicKey, User1Keys.PrivateKey));
            Assert.True(await DirectoryManager.InsertDirectory(
                User1Id,
                User1RootDirId,
                user1Directory1Name,
                user1Directory1Id));


            Assert.True(await DirectoryManager.Rename(User1Id, user1Directory1Id, "My cool new directory"));


            Assert.True(await DirectoryManager.InsertDirectory(
                User1Id,
                User1RootDirId,
                user1Directory1Name,
                user2Directory2Id));


            Assert.False(await DirectoryManager.InsertDirectory(
                User1Id,
                User1RootDirId,
                "My cool new directory",
                IdGenerator.NewId(IdType.Directory)));
        }

        [Fact]
        public async void DirectoryInsert_GetDirectoryMetadata()
        {

            Assert.True(await UserManager.InsertUser(User1Id, User1RootDirId, User1Email, User1HashedPassword, User1Salt, User1Keys.PublicKey, User1Keys.PrivateKey));
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

        [Fact]
        public async void DirectoryDelete_NotRootDir()
        {
            Assert.True(await UserManager.InsertUser(User1Id, User1RootDirId, User1Email, User1HashedPassword, User1Salt, User1Keys.PublicKey, User1Keys.PrivateKey));
            Assert.True(await UserManager.InsertUser(User2Id, User2RootDirId, User2Email, User2HashedPassword, User2Salt, User2Keys.PublicKey, User2Keys.PrivateKey));
            Assert.Equal(new string[0], await DirectoryManager.Delete(User1Id, User1RootDirId));

            var directory = await DirectoryManager.GetDirectoryMetadata(User1Id, User1RootDirId, null);
            Assert.NotNull(directory);
        }

        [Fact]
        public async void DirectoryDelete()
        {
            Assert.True(await UserManager.InsertUser(User1Id, User1RootDirId, User1Email, User1HashedPassword, User1Salt, User1Keys.PublicKey, User1Keys.PrivateKey));
            Assert.True(await UserManager.InsertUser(User2Id, User2RootDirId, User2Email, User2HashedPassword, User2Salt, User2Keys.PublicKey, User2Keys.PrivateKey));

            Assert.True(await DirectoryManager.InsertDirectory(
                User1Id,
                User1RootDirId,
                user1Directory1Name,
                user1Directory1Id));

            Assert.Equal(new string[0], await DirectoryManager.Delete(User1Id, user1Directory1Id));

            var directory = await DirectoryManager.GetDirectoryMetadata(User1Id, user1Directory1Id, null);
            Assert.Null(directory);
        }

        [Fact]
        public async void DirectoryGetPermissions()
        {
            Assert.True(await UserManager.InsertUser(User1Id, User1RootDirId, User1Email, User1HashedPassword, User1Salt, User1Keys.PublicKey, User1Keys.PrivateKey));
            Assert.True(await UserManager.InsertUser(User2Id, User2RootDirId, User2Email, User2HashedPassword, User2Salt, User2Keys.PublicKey, User2Keys.PrivateKey));

            Assert.True(await DirectoryManager.InsertDirectory(
                User1Id,
                User1RootDirId,
                user1Directory1Name,
                user1Directory1Id));

            var directoryPermissions = await DirectoryManager.GetPermissions(User1Id, User1Id,
                new[] { User1RootDirId, user1Directory1Id });

            var rootDirPermissions = directoryPermissions.First(x => x.ObjectId == User1RootDirId);
            var subDirPermissions = directoryPermissions.First(x => x.ObjectId == user1Directory1Id);

            Assert.Equal(rootDirPermissions.Permission, subDirPermissions.Permission);
            Assert.Equal(rootDirPermissions.Permission,
                        (Permission.ReadWrite |
                         Permission.Read |
                         Permission.ReadPermissions |
                         Permission.ReadWritePermissions)
            );

        }
    }
}
