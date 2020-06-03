using System;
using System.Collections.Generic;
using System.Linq;
using LessPaper.Guard.Database.MongoDb.Models.Dtos;
using LessPaper.Shared.Interfaces.General;

namespace LessPaper.Guard.Database.MongoDb.Models.Implement
{
    public class FileRevision : IFileRevision
    {
        private readonly FileRevisionDto dto;

        public FileRevision(FileRevisionDto dto)
        {
            this.dto = dto;

            AccessKeys = dto.AccessKeys.ToDictionary(x => x.UserId, x => (IAccessKey) new AccessKey(x));
        }

        /// <inheritdoc />
        public uint QuickNumber => dto.QuickNumber;


        /// <inheritdoc />
        public uint SizeInBytes => dto.SizeInBytes;

        /// <inheritdoc />
        public DateTime ChangeDate => dto.ChangeDate;

        /// <inheritdoc />
        public Dictionary<string, IAccessKey> AccessKeys { get; }

        /// <inheritdoc />
        public string ObjectId => dto.Id;
    }
}
