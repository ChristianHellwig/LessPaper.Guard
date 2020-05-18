using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using LessPaper.Shared.Enums;
using LessPaper.Shared.Helper;
using LessPaper.Shared.Interfaces.Database.Manager;

namespace LessPaper.GuardService.IntegrationTest
{
    public static class Extensions
    {
        public static async Task<BasicUser> GenerateUser(this IDbUserManager userManager)
        {
            var userId = IdGenerator.NewId(IdType.User);
            var rootDirId = IdGenerator.NewId(IdType.Directory);
            var hashedPassword = "HashedPassword";
            var salt = "Salt";
            var email = userId + "@test.corp";
            var keys = CryptoHelper.GenerateRsaKeyPair();

            await userManager.InsertUser(userId, rootDirId, email, hashedPassword, salt,
                keys.PublicKey, keys.PrivateKey);

            return new BasicUser(userId, rootDirId, email);
        }

    }
}
