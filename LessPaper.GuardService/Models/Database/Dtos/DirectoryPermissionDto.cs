using LessPaper.Shared.Enums;
using MongoDB.Driver;

namespace LessPaper.GuardService.Models.Database.Dtos
{
    public class DirectoryPermissionDto
    {
        public Permission Permission { get; set; }

        public MongoDBRef User { get; set; }
        
    }
}
