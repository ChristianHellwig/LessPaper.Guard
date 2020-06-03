using System;
using System.Threading.Tasks;
using LessPaper.GuardService.Models.Api;
using LessPaper.Shared.Enums;
using LessPaper.Shared.Helper;
using LessPaper.Shared.Interfaces.Database.Manager;
using LessPaper.Shared.Interfaces.General;
using LessPaper.Shared.Interfaces.GuardApi;
using LessPaper.Shared.Interfaces.GuardApi.Response;
using LessPaper.Shared.Interfaces.WriteApi.WriteObjectApi;
using LessPaper.Shared.Models.Exceptions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;

namespace LessPaper.GuardService.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class GuardController : ControllerBase
    {
        private readonly ILogger<GuardController> logger;
        private readonly IDbUserManager userManager;
        private readonly IDbDirectoryManager directoryManager;
        private readonly IDbFileManager fileManager;

        public GuardController(
            ILogger<GuardController> logger,
            IDbUserManager userManager,
            IDbDirectoryManager directoryManager,
            IDbFileManager fileManager)
        {
            this.logger = logger;
            this.userManager = userManager;
            this.directoryManager = directoryManager;
            this.fileManager = fileManager;
        }

        [HttpPost]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> RegisterNewUser(UserCreationRequest request)
        {
            if (!IdGenerator.IsType(request.UserId, IdType.User))
                return BadRequest();
            
            
            var rootDirectoryId = IdGenerator.NewId(IdType.Directory);

            try
            {
                var successful = await userManager.InsertUser(
                    request.UserId,
                    rootDirectoryId,
                    request.Email,
                    request.HashedPassword,
                    request.Salt,
                    request.PublicKey,
                    request.EncryptedPrivateKey);
                
                if (!successful)
                    return BadRequest();
            }
            catch (InvalidParameterException)
            {
                throw;
            }
            catch (ObjectNotResolvableException)
            {
                throw;
            }
            catch (UnexpectedBehaviourException)
            {
                throw;
            }
            catch (DatabaseException e)
            {
                throw new DatabaseException("Database error during file delete. See inner exception.", e);
            }
            catch (Exception e)
            {
                throw new Exception("Unknown error during file delete. See inner exception.", e);
            }



            return Ok();
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
