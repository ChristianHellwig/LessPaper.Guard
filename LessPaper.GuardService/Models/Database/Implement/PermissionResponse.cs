using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using LessPaper.Shared.Enums;
using LessPaper.Shared.Interfaces.GuardApi.Response;

namespace LessPaper.GuardService.Models.Database.Implement
{
    public class PermissionResponse : IPermissionResponse
    {
        public PermissionResponse(string objectId, Permission permission)
        {
            ObjectId = objectId;
            Permission = permission;
        }

        /// <inheritdoc />
        public string ObjectId { get; }

        /// <inheritdoc />
        public Permission Permission { get; }
    }
}
