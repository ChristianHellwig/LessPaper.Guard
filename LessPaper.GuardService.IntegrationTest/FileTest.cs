using System;
using System.Collections.Generic;
using System.Linq;
using System.Security;
using System.Text;
using System.Threading.Tasks;
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

        [Fact]
        public async void FileInsert()
        {
            var user1 = await UserManager.GenerateUser();
            var fileId = IdGenerator.NewId(IdType.File);
            var revisionId = IdGenerator.NewId(IdType.FileBlob);

            var keys = new Dictionary<string, string>()
            {
                { user1.UserId, "EncryptedKey" }
            };
            
            var quickNumberFile1 = await FileManager.InsertFile(
                user1.UserId,
               user1.Email,
                fileId,
                revisionId,
               "File1",
               2000000,
                keys,
               DocumentLanguage.English,
               ExtensionType.Pdf
           );

            var rootDirectory =
                await DirectoryManager.GetPermissions(user1.UserId, user1.UserId, new[] { user1.RootDirectoryId });
            var file1 = await FileManager.GetFileMetadata(user1.UserId, fileId, null);
            var fileRevision = file1.Revisions.First();

            Assert.Equal(fileId, file1.ObjectId);
            Assert.Single(file1.Permissions);
            Assert.Equal(
                rootDirectory.First(x => x.ObjectId == user1.RootDirectoryId).Permission,
                file1.Permissions[user1.UserId]);
            Assert.Single(file1.Revisions);
            Assert.Equal(1U, quickNumberFile1);
            Assert.Equal(revisionId, fileRevision.ObjectId);
            Assert.Equal("File1", file1.ObjectName);
            Assert.Equal(2000000, (int)fileRevision.SizeInBytes);
            Assert.Equal(1U, fileRevision.QuickNumber);
            Assert.Equal(DocumentLanguage.English, file1.Language);
            Assert.Equal(ExtensionType.Pdf, file1.Extension);
            Assert.Empty(file1.Tags);
        }


        //[Fact]
        public async void FileInsert_QuickNumber()
        {
            var user1 = await UserManager.GenerateUser();

            for (uint i = 0; i < 5; i++)
            {
                var fileId = IdGenerator.NewId(IdType.File);
                var revisionId = IdGenerator.NewId(IdType.FileBlob);

                var keys = new Dictionary<string, string>()
                {
                    { user1.UserId, "EncryptedKey" }
                };

                var quickNumberFile1 = await FileManager.InsertFile(
                    user1.UserId,
                    user1.Email,
                    fileId,
                    revisionId,
                    "File1",
                    2000000,
                    keys,
                    DocumentLanguage.English,
                    ExtensionType.Pdf
                );

                Assert.Equal(i, quickNumberFile1);
            }
        }

        //[Fact]
        public async void FileDelete()
        {
            var user1 = await UserManager.GenerateUser();
            var fileId = IdGenerator.NewId(IdType.File);
            var revisionId = IdGenerator.NewId(IdType.FileBlob);

            var keys = new Dictionary<string, string>()
            {
                { user1.UserId, "EncryptedKey" }
            };

            var quickNumberFile1 = await FileManager.InsertFile(
                user1.UserId,
                user1.Email,
                fileId,
                revisionId,
                "File1",
                2000000,
                keys,
                DocumentLanguage.English,
                ExtensionType.Pdf
            );

            var deletedBlobs = await FileManager.Delete(user1.UserId, fileId);
            Assert.Single(deletedBlobs);
            Assert.Equal(revisionId, deletedBlobs.First());
        }

        //[Fact]
        public async void FileGetPermissions()
        {
            var user1 = await UserManager.GenerateUser();
            var fileId = IdGenerator.NewId(IdType.File);
            var revisionId = IdGenerator.NewId(IdType.FileBlob);

            var keys = new Dictionary<string, string>()
            {
                { user1.UserId, "EncryptedKey" }
            };

            var quickNumberFile1 = await FileManager.InsertFile(
                user1.UserId,
                user1.Email,
                fileId,
                revisionId,
                "File1",
                2000000,
                keys,
                DocumentLanguage.English,
                ExtensionType.Pdf
            );


            var directoryMetadata = await DirectoryManager.GetDirectoryMetadata(user1.UserId, user1.RootDirectoryId, null);
            var fileMetadata = await FileManager.GetFileMetadata(user1.UserId, fileId, revisionId);
            
            Assert.Single(fileMetadata.Permissions);
            Assert.Single(directoryMetadata.Permissions);
            Assert.Equal(directoryMetadata.Permissions.First().Value, fileMetadata.Permissions.First().Value);
            Assert.Equal(
                Permission.ReadWrite | Permission.Read | Permission.ReadPermissions | Permission.ReadWritePermissions, 
                fileMetadata.Permissions.First().Value);

        }
    }
}
