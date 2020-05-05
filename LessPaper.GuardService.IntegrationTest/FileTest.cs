using System;
using System.Collections.Generic;
using System.Linq;
using System.Security;
using System.Text;
using LessPaper.GuardService.Models.Database.Implement;
using LessPaper.Shared.Enums;
using LessPaper.Shared.Helper;
using Microsoft.AspNetCore.Components.Forms;
using MongoDB.Bson.Serialization.Serializers;
using Xunit;

namespace LessPaper.GuardService.IntegrationTest
{
    public class FileTest : MongoTestBase
    {
        protected string User1Directory1Name = "Dir_1";
        protected string User1Directory1Id = IdGenerator.NewId(IdType.Directory);
        protected string User1FileName1 = "File1";
        protected string User1FileId1 = IdGenerator.NewId(IdType.File);
        protected string User1BlobId1 = IdGenerator.NewId(IdType.FileBlob);
        protected int User1FileSize1 = 5000000;
        protected string User1FilePlaintextKey1 = "MasterKey1";

        protected string User1Directory2Name = "Dir_1";
        protected string User1Directory2Id = IdGenerator.NewId(IdType.Directory);
        protected string User1FileName2 = "File1";
        protected string User1FileId2 = IdGenerator.NewId(IdType.File);
        protected string User1BlobId2 = IdGenerator.NewId(IdType.FileBlob);
        protected int User1FileSize2 = 5000000;
        protected string User1FilePlaintextKey2 = "MasterKey1";


        [Fact]
        public async void FileInsert()
        {
            Assert.True(await UserManager.InsertUser(User1Id, User1RootDirId, User1Email, User1HashedPassword, User1Salt, User1Keys.PublicKey, User1Keys.PrivateKey));
            var quickNumberFile1 = await FileManager.InsertFile(
                User1Id,
                User1RootDirId,
                User1FileId1,
                User1BlobId1,
                User1FileName1,
                User1FileSize1,
                User1FilePlaintextKey1,
                DocumentLanguage.English,
                ExtensionType.Pdf
            );

            var rootDirectory =
                await DirectoryManager.GetPermissions(User1Id, User1Id, new[] {User1RootDirId});
            var file1 = await FileManager.GetFileMetadata(User1Id, User1FileId1, null);
            var fileRevision = file1.Revisions.First();

            Assert.Equal(User1FileId1, file1.ObjectId);
            Assert.Single(file1.Permissions);
            Assert.Equal(
                rootDirectory.First(x => x.ObjectId == User1RootDirId).Permission,
                file1.Permissions[User1Id]);
            Assert.Single(file1.Revisions);
            Assert.Equal(1U, quickNumberFile1);
            Assert.Equal(User1BlobId1, fileRevision.BlobId);
            Assert.Equal(User1FileName1, file1.ObjectName);
            Assert.Equal(User1FileSize1, (int)fileRevision.SizeInBytes);
            Assert.Equal(1U, file1.QuickNumber);
            Assert.Equal(DocumentLanguage.English, file1.Language);
            Assert.Equal(ExtensionType.Pdf, file1.Extension);
            Assert.Empty(file1.Tags);
        }


        //[Fact]
        public async void FileInsert_QuickNumber()
        {
            Assert.True(await UserManager.InsertUser(User1Id, User1RootDirId, User1Email, User1HashedPassword, User1Salt, User1Keys.PublicKey, User1Keys.PrivateKey));
            var quickNumberFile1 = await FileManager.InsertFile(
                User1Id,
                User1RootDirId,
                User1FileId1,
                User1BlobId1,
                User1FileName1,
                User1FileSize1,
                User1FilePlaintextKey1,
                DocumentLanguage.German,
                ExtensionType.Docx
            );
            Assert.Equal(0U, quickNumberFile1);

            var quickNumberFile2 = await FileManager.InsertFile(
                User1Id,
                User1RootDirId,
                User1FileId2,
                User1BlobId2,
                User1FileName2,
                User1FileSize2,
                User1FilePlaintextKey2,
                DocumentLanguage.German,
                ExtensionType.Docx
            );
            Assert.Equal(1U, quickNumberFile2);


            var file1 = await FileManager.GetFileMetadata(User1Id, User1FileId1, null);
            
        }

        //[Fact]
        public async void FileDelete()
        {
            Assert.True(await UserManager.InsertUser(User1Id, User1RootDirId, User1Email, User1HashedPassword, User1Salt, User1Keys.PublicKey, User1Keys.PrivateKey));
            Assert.True(await UserManager.InsertUser(User2Id, User2RootDirId, User2Email, User2HashedPassword, User2Salt, User2Keys.PublicKey, User2Keys.PrivateKey));

            Assert.True(await DirectoryManager.InsertDirectory(
                User1Id,
                User1RootDirId,
                User1Directory1Name,
                User1Directory1Id));

            Assert.Equal(new string[0], await DirectoryManager.Delete(User1Id, User1Directory1Id));

            var directory = await DirectoryManager.GetDirectoryMetadata(User1Id, User1Directory1Id, null);
            Assert.Null(directory);
        }

        //[Fact]
        public async void FileGetPermissions()
        {
            Assert.True(await UserManager.InsertUser(User1Id, User1RootDirId, User1Email, User1HashedPassword, User1Salt, User2Keys.PublicKey, User2Keys.PrivateKey));
            Assert.True(await UserManager.InsertUser(User2Id, User2RootDirId, User2Email, User2HashedPassword, User2Salt, User2Keys.PublicKey, User2Keys.PrivateKey));

            Assert.True(await DirectoryManager.InsertDirectory(
                User1Id,
                User1RootDirId,
                User1Directory1Name,
                User1Directory1Id));

            var directoryPermissions = await DirectoryManager.GetPermissions(User1Id, User1Id,
                new[] {User1RootDirId, User1Directory1Id});

            var rootDirPermissions = directoryPermissions.First(x => x.ObjectId == User1RootDirId);
            var subDirPermissions = directoryPermissions.First(x => x.ObjectId == User1Directory1Id);

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
