using LessPaper.Shared.Enums;

namespace LessPaper.Guard.Database.MongoDb.Models.Dtos
{
    public class BasicPermissionDto
    {
        public Permission Permission { get; set; }

        public string UserId { get; set; }
        
    }
}
