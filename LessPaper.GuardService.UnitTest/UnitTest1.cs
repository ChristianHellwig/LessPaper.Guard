using System;

using LessPaper.Shared.Helper;
using Xunit;

namespace LessPaper.GuardService.UnitTest
{
    public class UnitTest1
    {
        [Fact]
        public async void Test1()
        {
            var kp = CryptoHelper.GenerateRsaKeyPair();

            for (int i = 0; i < 10000; i++)
            {
                var e = CryptoHelper.RsaEncrypt(kp.PublicKey, CryptoHelper.GetSalt(32));
            }

            Console.WriteLine("d");



            //var x = new DatabaseManager();

            //var userId = IdGenerator.NewId(IdType.UserId);
            //var rootId = IdGenerator.NewId(IdType.Directory);
            //var subDirId = IdGenerator.NewId(IdType.Directory);
            //var subDirId2 = IdGenerator.NewId(IdType.Directory);

            //await x.DbUserManager.InsertUser(userId, rootId, "a@b.de", "hash", "salt");

            //await x.DbDirectoryManager.InsertDirectory(userId, rootId, "Dir1", subDirId);
            //await x.DbDirectoryManager.InsertDirectory(userId, rootId, "Dir2", subDirId2);

            //await x.DbDirectoryManager.GetDirectoryMetadata(userId, rootId, 0);

            //await x.DbDirectoryManager.GetDirectoryPermissions(userId, userId, new[] {rootId, subDirId2});


            //var fileId = IdGenerator.NewId(IdType.File);
            //await x.DbFileManager.InsertFile(
            //    userId, 
            //    rootId, 
            //    fileId, 
            //    "myblob",
            //    "myDoc",
            //    10,
            //    CryptoHelper.GetSalt(16),
            //    DocumentLanguage.German,
            //    ExtensionType.Docx);

            //var qq = await x.DbFileManager.GetFilePermissions(userId, userId, new[] {fileId});
            //var res = await x.DbFileManager.GetFileMetadata(userId, fileId, 0);


            //await x.DbDirectoryManager.DeleteDirectory(userId, subDirId);
        }
    }
}
