using System.Collections.Generic;
using System.Linq;
using LessPaper.Shared.Enums;
using LessPaper.Shared.Helper;
using Xunit;

namespace LessPaper.Guard.Database.MongoDb.IntegrationTest
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
               user1.RootDirectoryId,
                fileId,
                revisionId,
               "File1",
               2000000,
                keys,
               DocumentLanguage.English,
               ExtensionType.Pdf
           );

            Assert.Equal(1u, quickNumberFile1);

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
            Assert.Equal($"/{user1.RootDirectoryId}/{fileId}", file1.Path);
        }


        [Fact]
        public async void FileInsert_QuickNumber()
        {
            var user1 = await UserManager.GenerateUser();

            for (uint i = 1; i <= 5; i++)
            {
                var fileId = IdGenerator.NewId(IdType.File);
                var revisionId = IdGenerator.NewId(IdType.FileBlob);

                var keys = new Dictionary<string, string>()
                {
                    { user1.UserId, "EncryptedKey" }
                };

                var quickNumberFile1 = await FileManager.InsertFile(
                    user1.UserId,
                    user1.RootDirectoryId,
                    fileId,
                    revisionId,
                    "File" + i,
                    2000000,
                    keys,
                    DocumentLanguage.English,
                    ExtensionType.Pdf
                );

                Assert.Equal(i, quickNumberFile1);
            }
        }

        [Fact]
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
                user1.RootDirectoryId,
                fileId,
                revisionId,
                "File1",
                2000000,
                keys,
                DocumentLanguage.English,
                ExtensionType.Pdf
            );

            Assert.NotEqual(0u, quickNumberFile1);
            var deletedBlobs = await FileManager.Delete(user1.UserId, fileId);
            Assert.Single(deletedBlobs);
            Assert.Equal(revisionId, deletedBlobs.First());
        }

        [Fact]
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
                user1.RootDirectoryId,
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

        [Fact]
        public async void FileMove()
        {
            var user1 = await UserManager.GenerateUser();
            var subDirectoryId = IdGenerator.NewId(IdType.Directory);

            Assert.True(await DirectoryManager.InsertDirectory(
                user1.UserId, 
                user1.RootDirectoryId, 
                "Dir1", 
                subDirectoryId));

            var fileId = IdGenerator.NewId(IdType.File);
            var revisionId = IdGenerator.NewId(IdType.FileBlob);

            var keys = new Dictionary<string, string>()
            {
                { user1.UserId, "EncryptedKey" }
            };

            var quickNumberFile1 = await FileManager.InsertFile(
                user1.UserId,
                user1.RootDirectoryId,
                fileId,
                revisionId,
                "File1",
                2000000,
                keys,
                DocumentLanguage.English,
                ExtensionType.Pdf
            );

            // Check if root directory contains the file
            var directoryMetadata = await DirectoryManager.GetDirectoryMetadata(user1.UserId, user1.RootDirectoryId, null);
            Assert.Single(directoryMetadata.FileChilds);
            Assert.Equal(fileId, directoryMetadata.FileChilds.First().ObjectId);

            // Check if path is correct
            var fileMetadata = await FileManager.GetFileMetadata(user1.UserId, fileId, revisionId);
            Assert.Equal($"/{user1.RootDirectoryId}/{fileId}", fileMetadata.Path);
            
            // Move file to sub directory
            Assert.True(await FileManager.Move(user1.UserId, fileId, subDirectoryId));

            directoryMetadata = await DirectoryManager.GetDirectoryMetadata(user1.UserId, subDirectoryId, null);
            fileMetadata = await FileManager.GetFileMetadata(user1.UserId, fileId, revisionId);

            Assert.Single(directoryMetadata.FileChilds);
            Assert.Equal(fileId, directoryMetadata.FileChilds.First().ObjectId);
            Assert.Single(fileMetadata.Permissions);
            Assert.Single(directoryMetadata.Permissions);
            Assert.Equal(directoryMetadata.Permissions.First().Value, fileMetadata.Permissions.First().Value);
            Assert.Equal(
                Permission.ReadWrite | Permission.Read | Permission.ReadPermissions | Permission.ReadWritePermissions,
                fileMetadata.Permissions.First().Value);
            Assert.Equal($"/{user1.RootDirectoryId}/{subDirectoryId}/{fileId}", fileMetadata.Path);

        }

    }
}
