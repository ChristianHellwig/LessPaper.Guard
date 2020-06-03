using LessPaper.Shared.Enums;
using LessPaper.Shared.Interfaces.GuardApi.Response;

namespace LessPaper.Guard.Database.MongoDb.Models.Implement
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
