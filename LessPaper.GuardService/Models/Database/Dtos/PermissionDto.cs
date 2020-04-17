using LessPaper.Shared.Enums;

namespace LessPaper.GuardService.Models.Database.Dtos
{
    public class PermissionDto
    {
        public Permissions Permission { get; set; }

        public UserDto User { get; set; }
        
    }
}
