using System;
using LessPaper.Shared.Enums;
using LessPaper.Shared.Helper;
using LessPaper.Shared.Models.Exceptions;
using Xunit;
using System.Linq;

namespace LessPaper.Guard.Database.MongoDb.IntegrationTest
{
    public class DirectoryTest : MongoTestBase
    {

        [Fact]
        public async void DirectoryShare()
        {
            var user1 = await UserManager.GenerateUser();
            var user2 = await UserManager.GenerateUser();
            
            var idA = IdGenerator.NewId(IdType.Directory);
            Assert.True(await DirectoryManager.InsertDirectory(
                user1.UserId,
                user1.RootDirectoryId,
                "A",
                idA));

            var idB = IdGenerator.NewId(IdType.Directory);
            Assert.True(await DirectoryManager.InsertDirectory(
                user1.UserId,
                user1.RootDirectoryId,
                "B",
                idB));

            var idC = IdGenerator.NewId(IdType.Directory);
            Assert.True(await DirectoryManager.InsertDirectory(
                user1.UserId,
                idA,
                "C",
                idC));
            
            var prepareShareData = await DirectoryManager.PrepareShare(
                user1.UserId, 
                idC, 
                new[] { user2.Email });

            Assert.Equal(idC, prepareShareData.RequestedObjectId);
            Assert.Empty(prepareShareData.Files);
            Assert.Single(prepareShareData.PublicKeys);
            Assert.Equal(user2.Email, prepareShareData.PublicKeys.Keys.First());

        }

        [Fact]
        public async void DirectoryMove()
        {
            var user1 = await UserManager.GenerateUser();

            var idA = IdGenerator.NewId(IdType.Directory);
            var idB = IdGenerator.NewId(IdType.Directory);
            var idC = IdGenerator.NewId(IdType.Directory);

            // Root
            // | \
            // A  B
            // | 
            // C

            Assert.True(await DirectoryManager.InsertDirectory(
                user1.UserId,
                user1.RootDirectoryId,
                "A",
                idA));


            Assert.True(await DirectoryManager.InsertDirectory(
                user1.UserId,
                user1.RootDirectoryId,
                "B",
                idB));

            Assert.True(await DirectoryManager.InsertDirectory(
                user1.UserId,
                idA,
                "C",
                idC));


            var rootDirectory = await DirectoryManager.GetDirectoryMetadata(user1.UserId, user1.RootDirectoryId, null);
            Assert.Equal(2, rootDirectory.DirectoryChilds.Length);
            Assert.Contains(rootDirectory.DirectoryChilds, x => x.ObjectId == idA);
            Assert.Contains(rootDirectory.DirectoryChilds, x => x.ObjectId == idB);
            Assert.Equal($"/{user1.RootDirectoryId}", rootDirectory.Path);

            var aDirectory = await DirectoryManager.GetDirectoryMetadata(user1.UserId, idA, null);
            Assert.Single(aDirectory.DirectoryChilds);
            Assert.Contains(aDirectory.DirectoryChilds, x => x.ObjectId == idC);
            Assert.Equal($"/{user1.RootDirectoryId}/{idA}", aDirectory.Path);

            var bDirectory = await DirectoryManager.GetDirectoryMetadata(user1.UserId, idB, null);
            Assert.Empty(bDirectory.DirectoryChilds);
            Assert.Equal($"/{user1.RootDirectoryId}/{idB}", bDirectory.Path);

            var cDirectory = await DirectoryManager.GetDirectoryMetadata(user1.UserId, idC, null);
            Assert.Empty(cDirectory.DirectoryChilds);
            Assert.Equal($"/{user1.RootDirectoryId}/{idA}/{idC}", cDirectory.Path);


            var st = DateTime.UtcNow;
            Assert.True(await DirectoryManager.Move(user1.UserId, idA, idB));
            var dur = DateTime.UtcNow - st;

            // Root
            // |
            // B 
            // |  
            // A
            // |
            // C

            rootDirectory = await DirectoryManager.GetDirectoryMetadata(user1.UserId, user1.RootDirectoryId, null);
            Assert.Single(rootDirectory.DirectoryChilds);
            Assert.Contains(rootDirectory.DirectoryChilds, x => x.ObjectId == idB);
            Assert.Equal($"/{user1.RootDirectoryId}", rootDirectory.Path);

            aDirectory = await DirectoryManager.GetDirectoryMetadata(user1.UserId, idA, null);
            Assert.Single(aDirectory.DirectoryChilds);
            Assert.Contains(aDirectory.DirectoryChilds, x => x.ObjectId == idC);
            Assert.Equal($"/{user1.RootDirectoryId}/{idB}/{idA}", aDirectory.Path);

            bDirectory = await DirectoryManager.GetDirectoryMetadata(user1.UserId, idB, null);
            Assert.Single(bDirectory.DirectoryChilds);
            Assert.Contains(bDirectory.DirectoryChilds, x => x.ObjectId == idA);
            Assert.Equal($"/{user1.RootDirectoryId}/{idB}", bDirectory.Path);

            cDirectory = await DirectoryManager.GetDirectoryMetadata(user1.UserId, idC, null);
            Assert.Empty(cDirectory.DirectoryChilds);
            Assert.Equal($"/{user1.RootDirectoryId}/{idB}/{idA}/{idC}", cDirectory.Path);
        }

        [Fact]
        public async void DirectoryInsert_SameName()
        {
            var user1 = await UserManager.GenerateUser();
            
            Assert.True(await DirectoryManager.InsertDirectory(
                user1.UserId,
                user1.RootDirectoryId,
                "Duplicate_Name",
                IdGenerator.NewId(IdType.Directory)));

            await Assert.ThrowsAsync<DatabaseException>(async () =>
                await DirectoryManager.InsertDirectory(
                     user1.UserId,
                     user1.RootDirectoryId,
                     "Duplicate_Name",
                     IdGenerator.NewId(IdType.Directory))
            );
        }



        [Fact]
        public async void DirectoryInsert_Rename()
        {
            var user1 = await UserManager.GenerateUser();

            var subDirectoryId = IdGenerator.NewId(IdType.Directory);
            Assert.True(await DirectoryManager.InsertDirectory(
                user1.UserId,
                user1.RootDirectoryId,
                "Dir1",
                subDirectoryId
               ));


            // Rename directory
            Assert.True(await DirectoryManager.Rename(user1.UserId, subDirectoryId, "Renamed dir!"));

            // Try to insert new directory with old name
            Assert.True(await DirectoryManager.InsertDirectory(
                user1.UserId,
                user1.RootDirectoryId,
                "Dir1",
                IdGenerator.NewId(IdType.Directory)));


            // Ensure metadata is updated
            var metadata = await DirectoryManager.GetDirectoryMetadata(user1.UserId, subDirectoryId, null);
            Assert.Equal("Renamed dir!", metadata.ObjectName);
        }

        [Fact]
        public async void DirectoryInsert_GetDirectoryMetadata()
        {
            var user1 = await UserManager.GenerateUser();
            var subDirectoryId = IdGenerator.NewId(IdType.Directory);

            Assert.True(await DirectoryManager.InsertDirectory(
                user1.UserId,
                user1.RootDirectoryId,
                "Dir1",
                subDirectoryId));
            
            var rootDirectoryMetadata = await DirectoryManager.GetDirectoryMetadata(user1.UserId, user1.RootDirectoryId, null);
            Assert.Single(rootDirectoryMetadata.DirectoryChilds);
            Assert.Equal(1u, rootDirectoryMetadata.NumberOfChilds);
            var subDirectoryMinimalMetadata = rootDirectoryMetadata.DirectoryChilds.First();
            Assert.Equal(subDirectoryId, subDirectoryMinimalMetadata.ObjectId);
            Assert.Equal("Dir1", subDirectoryMinimalMetadata.ObjectName);
            Assert.Single(subDirectoryMinimalMetadata.Permissions);
            Assert.Equal(0u, subDirectoryMinimalMetadata.NumberOfChilds);
            Assert.Equal($"/{user1.RootDirectoryId}", rootDirectoryMetadata.Path);
            Assert.True(subDirectoryMinimalMetadata.Permissions[user1.UserId] ==
                        (Permission.ReadWrite |
                         Permission.Read |
                         Permission.ReadPermissions |
                         Permission.ReadWritePermissions)
            );
            

            var subDirectoryMetadata = await DirectoryManager.GetDirectoryMetadata(user1.UserId, subDirectoryId, null);
            Assert.Equal(subDirectoryId, subDirectoryMetadata.ObjectId);
            Assert.Equal("Dir1", subDirectoryMetadata.ObjectName);
            Assert.Equal($"/{user1.RootDirectoryId}/{subDirectoryId}", subDirectoryMetadata.Path);
            Assert.Empty(subDirectoryMetadata.DirectoryChilds);
            Assert.Empty(subDirectoryMetadata.FileChilds);
            Assert.Single(subDirectoryMetadata.Permissions);
            Assert.Equal(0u, subDirectoryMinimalMetadata.NumberOfChilds);
            Assert.True(subDirectoryMetadata.Permissions[user1.UserId] ==
                (Permission.ReadWrite |
                Permission.Read |
                Permission.ReadPermissions |
                Permission.ReadWritePermissions)
            );
        }

        [Fact]
        public async void DirectoryDelete_RootDir_Should_Not_Be_Deletable()
        {
            var user1 = await UserManager.GenerateUser();

            await Assert.ThrowsAsync<ObjectNotResolvableException>(async () => 
                await DirectoryManager.Delete(user1.UserId, user1.RootDirectoryId)
            );
          
            var metadata = await DirectoryManager.GetDirectoryMetadata(user1.UserId, user1.RootDirectoryId, null);
            Assert.NotNull(metadata);
        }

        [Fact]
        public async void DirectoryDelete()
        {
            var user1 = await UserManager.GenerateUser();
            var subDirectoryId = IdGenerator.NewId(IdType.Directory);

            Assert.True(await DirectoryManager.InsertDirectory(
                user1.UserId,
                user1.RootDirectoryId,
                "Dir1",
                subDirectoryId));

            Assert.Equal(new string[0], await DirectoryManager.Delete(user1.UserId, subDirectoryId));


            await Assert.ThrowsAsync<ObjectNotResolvableException>(
                async () => await DirectoryManager.GetDirectoryMetadata(user1.UserId, subDirectoryId, null));

        }

        [Fact]
        public async void DirectoryGetPermissions()
        {
            var user1 = await UserManager.GenerateUser();
            var subDirectoryId = IdGenerator.NewId(IdType.Directory);

            Assert.True(await DirectoryManager.InsertDirectory(
                user1.UserId,
                user1.RootDirectoryId,
                "Dir1",
                subDirectoryId));

            var directoryPermissions = await DirectoryManager.GetPermissions(
                user1.UserId,
                user1.UserId,
                new[] { user1.RootDirectoryId, subDirectoryId });

            var rootDirPermissions = directoryPermissions.First(x => x.ObjectId == user1.RootDirectoryId);
            var subDirPermissions = directoryPermissions.First(x => x.ObjectId == subDirectoryId);

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
