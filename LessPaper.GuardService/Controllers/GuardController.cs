using System;
using System.Threading.Tasks;
using LessPaper.Shared.Enums;
using LessPaper.Shared.Interfaces.General;
using LessPaper.Shared.Interfaces.GuardApi;
using LessPaper.Shared.Interfaces.GuardApi.Response;
using LessPaper.Shared.Interfaces.WriteApi.WriteObjectApi;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace LessPaper.GuardService.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class GuardController : ControllerBase, IGuardApi
    {
        private readonly ILogger<GuardController> _logger;

        public GuardController(ILogger<GuardController> logger)
        {
            _logger = logger;
        }

        /// <inheritdoc />
        public async Task<bool> RegisterNewUser(string email, string passwordHash, string salt, string userId)
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc />
        public async Task<IMinimalUserInformation> GetUserCredentials(string email)
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc />
        public async Task<IPermissionResponse[]> GetObjectsPermissions(string requestingUserId, string userId, string[] objectIds)
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc />
        public async Task<bool> AddDirectory(string requestingUserId, string parentDirectoryId, string directoryName, string newDirectoryId)
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc />
        public async Task<bool> DeleteObject(string requestingUserId, string objectId)
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc />
        public async Task<int> AddFile(string requestingUserId, string directoryId, string fileId, string blobId, string fileName, int fileSize,
            string encryptedKey, DocumentLanguage documentLanguage, ExtensionType fileExtension)
        {
            throw new NotImplementedException();
        }


        /// <inheritdoc />
        public async Task<bool> UpdateObjectMetadata(string requestingUserId, string objectId, IMetadataUpdate updatedMetadata)
        {
            //var directoryIds = new List<string>();
            //var fileIds = new List<string>();

            //var uniqueIds = new HashSet<string>(objectIds);

            //foreach (var objectId in uniqueIds)
            //{
            //    if (!IdGenerator.TypeFromId(objectId, out var type))
            //        continue;

            //    switch (type)
            //    {
            //        case IdType.Directory:
            //            directoryIds.Add(objectId);
            //            break;
            //        case IdType.File:
            //            fileIds.Add(objectId);
            //            break;
            //    }
            //}


            throw new NotImplementedException();
        }

        /// <inheritdoc />
        public async Task<IMetadata> GetMetadata(string requestingUserId, string objectId, uint? revisionNumber)
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc />
        public async Task<ISearchResult> Search(string requestingUserId, string directoryId, string searchQuery, uint count, uint page)
        {
            throw new NotImplementedException();
        }
    }
}
