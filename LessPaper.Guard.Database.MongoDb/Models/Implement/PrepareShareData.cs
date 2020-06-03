using System.Collections.Generic;
using System.Linq;
using LessPaper.Guard.Database.MongoDb.Models.Dtos;
using LessPaper.Shared.Interfaces.Database.Manager;
using LessPaper.Shared.Interfaces.General;

namespace LessPaper.Guard.Database.MongoDb.Models.Implement
{
    public class PrepareShareData : IPrepareShareData
    {
        public PrepareShareData(string requestedObjectId, Dictionary<string, string> publicKeys, IPrepareShareFile[] files)
        {
            PublicKeys = publicKeys;
            Files = files;
            RequestedObjectId = requestedObjectId;
        }

        /// <inheritdoc />
        public string RequestedObjectId { get; }

        /// <inheritdoc />
        public Dictionary<string, string> PublicKeys { get; }

        /// <inheritdoc />
        public IPrepareShareFile[] Files { get; }
    }

    public class PrepareShareFile : IPrepareShareFile
    {
        public PrepareShareFile(string fileId, IPrepareShareRevision[] revisions)
        {
            FileId = fileId;
            Revisions = revisions;
        }

        /// <inheritdoc />
        public string FileId { get; set; }

        /// <inheritdoc />
        public IPrepareShareRevision[] Revisions { get; set; }
    }

    public class PrepareShareRevision : IPrepareShareRevision
    {
        public PrepareShareRevision(string revisionId, AccessKeyDto[] accessKey)
        {
            RevisionId = revisionId;
            AccessKeys = accessKey.ToDictionary(x => x.UserId, x => (IAccessKey)new AccessKey(x));
        }

        /// <inheritdoc />
        public string RevisionId { get; set; }

        /// <inheritdoc />
        public Dictionary<string, IAccessKey> AccessKeys { get; }

    }

}
