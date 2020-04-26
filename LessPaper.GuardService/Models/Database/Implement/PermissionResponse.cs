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
        /// <inheritdoc />
        public string ObjectId { get; }

        /// <inheritdoc />
        public Permission Permission { get; }
    }
}
