using LessPaper.Shared.Interfaces.General;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Driver;

namespace LessPaper.GuardService.Models.Database.Dtos
{
    public class UserDto : BaseDto
    {
        public string Email { get; set; }
        
        public string Salt { get; set; }
        
        public string PasswordHash { get; set; }
        
        public MongoDBRef RootDirectory { get; set; }

        public string PublicKey { get; set; }
        public uint QuickNumber { get; set; }
    }
}
