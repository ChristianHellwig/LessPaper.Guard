using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using LessPaper.GuardService.Models.Database.Dtos;
using LessPaper.Shared.Enums;
using LessPaper.Shared.Interfaces.GuardApi.Response;

namespace LessPaper.GuardService.Models.Database.Implement
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
