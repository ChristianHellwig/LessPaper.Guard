using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using LessPaper.Shared.Enums;
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

        public async Task<bool> RegisterNewUser(string email, string passwordHash, string salt, string userId)
        {
            throw new NotImplementedException();
        }

        public async Task<ICredentialsResponse> GetUserCredentials(string email)
        {
            throw new NotImplementedException();
        }

        public async Task<IPermissionResponse> GetObjectsPermissions(string userId, string[] objectIds)
        {
            throw new NotImplementedException();
        }

        public async Task<bool> AddDirectory(string parentDirectoryId, string directoryName, string newDirectoryId)
        {
            throw new NotImplementedException();
        }

        public async Task<bool> DeleteObject(string objectId)
        {
            throw new NotImplementedException();
        }

        public async Task<int> AddFile(string directoryId, string fileId, int fileSize, string encryptedKey, DocumentLanguage documentLanguage,
            ExtensionType fileExtension)
        {
            throw new NotImplementedException();
        }

        public async Task<bool> UpdateObjectMetadata(string objectId, IMetadataUpdate updatedMetadata)
        {
            throw new NotImplementedException();
        }
    }
}
