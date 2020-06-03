using LessPaper.Guard.Database.MongoDb.Models.Dtos;
using LessPaper.Shared.Enums;

namespace LessPaper.Guard.Database.MongoDb.Models.Implement
{
    public class PermissionBo 
    {
        private readonly BasicPermissionDto dto;

        public PermissionBo(BasicPermissionDto dto)
        {
            this.dto = dto;
        }

        public string ObjectId => dto.UserId;

        public Permission Permission => dto.Permission;
    }
}
