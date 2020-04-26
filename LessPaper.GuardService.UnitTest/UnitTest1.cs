using System;
using System.Runtime.InteropServices;
using LessPaper.GuardService.Models.Database;
using LessPaper.Shared.Enums;
using LessPaper.Shared.Helper;
using Xunit;

namespace LessPaper.GuardService.UnitTest
{
    public class UnitTest1
    {
        [Fact]
        public async void Test1()
        {
            var x = new DatabaseManager();
            
            var userId = IdGenerator.NewId(IdType.User);
            var rootId = IdGenerator.NewId(IdType.Directory);
            var subDirId = IdGenerator.NewId(IdType.Directory);
            var subDirId2 = IdGenerator.NewId(IdType.Directory);

            await x.DbUserManager.InsertUser(userId, rootId, "a@b.de", "hash", "salt");

            await x.DbDirectoryManager.InsertDirectory(userId, rootId, "Dir1", subDirId);
            await x.DbDirectoryManager.InsertDirectory(userId, rootId, "Dir2", subDirId2);

            await x.DbDirectoryManager.GetDirectoryMetadata(userId, rootId, 0);

            await x.DbDirectoryManager.GetDirectoryPermissions(userId, userId, new[] {rootId, subDirId2});


            var fileId = IdGenerator.NewId(IdType.File);
            await x.DbFileManager.InsertFile(
                userId, 
                rootId, 
                fileId, 
                "myblob",
                "myDoc",
                10,
                CryptoHelper.GetSalt(32),
                DocumentLanguage.German,
                ExtensionType.Docx);

            var res = await x.DbFileManager.GetFileMetadata(userId, fileId, 0);

            
            await x.DbDirectoryManager.DeleteDirectory("MY_USER_ID", "ROOT_DIR_ID");
        }
    }
}
