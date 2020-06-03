using LessPaper.Shared.Enums;
using LessPaper.Shared.Helper;
using LessPaper.Shared.Models.Exceptions;
using Xunit;

namespace LessPaper.Guard.Database.MongoDb.IntegrationTest
{
    public class UserTest : MongoTestBase
    {
        protected string User1Id = IdGenerator.NewId(IdType.User);
        protected string User1RootDirId = IdGenerator.NewId(IdType.Directory);
        protected string User1HashedPassword = "HashedPassword";
        protected string User1Salt = "Salt";
        protected string User1Email;
        protected CryptoHelper.RsaKeyPair User1Keys = CryptoHelper.GenerateRsaKeyPair();

        protected string User2Id = IdGenerator.NewId(IdType.User);
        protected string User2RootDirId = IdGenerator.NewId(IdType.Directory);
        protected string User2HashedPassword = "HashedPassword";
        protected string User2Salt = "Salt";
        protected string User2Email;
        protected CryptoHelper.RsaKeyPair User2Keys = CryptoHelper.GenerateRsaKeyPair();

        public UserTest()
        {
            User1Email = User1Id + "@test.corp"; 
            User2Email = User2Id + "@test.corp";
        }

        [Fact]
        public async void UserDuplicateEmail()
        {
            Assert.True(await UserManager.InsertUser(User1Id, User1RootDirId, User1Email, User1HashedPassword, User1Salt, User1Keys.PublicKey, User1Keys.PrivateKey));
            await Assert.ThrowsAsync<DatabaseException>(async () => await UserManager.InsertUser(User2Id, User2RootDirId, User1Email, User2HashedPassword, User2Salt, User2Keys.PublicKey, User2Keys.PrivateKey));
        }

        [Fact]
        public async void UserDuplicateUserId()
        {
            Assert.True(await UserManager.InsertUser(User1Id, User1RootDirId, User1Email, User1HashedPassword, User1Salt, User1Keys.PublicKey, User1Keys.PrivateKey));
            await Assert.ThrowsAsync<DatabaseException>(async () => await UserManager.InsertUser(User1Id, User2RootDirId, User2Email, User2HashedPassword, User2Salt, User2Keys.PublicKey, User2Keys.PrivateKey));
        }

        [Fact]
        public async void UserDuplicateRootDirectoryId()
        {
            Assert.True(await UserManager.InsertUser(User1Id, User1RootDirId, User1Email, User1HashedPassword, User1Salt, User1Keys.PublicKey, User1Keys.PrivateKey));
            await Assert.ThrowsAsync<DatabaseException>(async () => await UserManager.InsertUser(User2Id, User1RootDirId, User2Email, User2HashedPassword, User2Salt, User2Keys.PublicKey, User2Keys.PrivateKey));
            await Assert.ThrowsAsync<ObjectNotResolvableException>(async () => await UserManager.GetBasicUserInformation(User2Id, User2Id));
        }

        
        [Fact]
        public async void UserDelete()
        {
            Assert.True(await UserManager.InsertUser(User1Id, User1RootDirId, User1Email, User1HashedPassword, User1Salt, User1Keys.PublicKey, User1Keys.PrivateKey));
            Assert.Equal(new string[0], await UserManager.DeleteUser(User1Id, User1Id));
        }


        [Fact]
        public async void UserDeleteByOtherUser()
        {
            Assert.True(await UserManager.InsertUser(User1Id, User1RootDirId, User1Email, User1HashedPassword, User1Salt, User1Keys.PublicKey, User1Keys.PrivateKey));
            Assert.True(await UserManager.InsertUser(User2Id, User2RootDirId, User2Email, User2HashedPassword, User2Salt, User2Keys.PublicKey, User2Keys.PrivateKey));
            Assert.Null(await UserManager.DeleteUser(User1Id, User2Id));
        }


        [Fact]
        public async void UserDeleteNonExisting()
        { 
            Assert.Empty(await UserManager.DeleteUser(User1Id, User1Id));
        }


        [Fact]
        public async void UserGetInfo()
        {
            Assert.True(await UserManager.InsertUser(User1Id, User1RootDirId, User1Email, User1HashedPassword, User1Salt, User1Keys.PublicKey, User1Keys.PrivateKey));
            Assert.True(await UserManager.InsertUser(User2Id, User2RootDirId, User2Email, User2HashedPassword, User2Salt, User2Keys.PublicKey, User2Keys.PrivateKey));

            var user = await UserManager.GetBasicUserInformation(User1Id, User1Id);
            Assert.Equal(User1Email, user.Email);
            Assert.Equal(User1HashedPassword, user.PasswordHash);
            Assert.Equal(User1RootDirId, user.RootDirectoryId);
            Assert.Equal(User1Salt, user.Salt);
        }

        [Fact]
        public async void UserGetInfoFromOtherUser()
        {
            Assert.True(await UserManager.InsertUser(User1Id, User1RootDirId, User1Email, User1HashedPassword, User1Salt, User1Keys.PublicKey, User1Keys.PrivateKey));
            Assert.True(await UserManager.InsertUser(User2Id, User2RootDirId, User2Email, User2HashedPassword, User2Salt, User2Keys.PublicKey, User2Keys.PrivateKey));

            await Assert.ThrowsAsync<ObjectNotResolvableException>(async () => await UserManager.GetBasicUserInformation(User1Id, User2Id));
      
        }
    }
}
