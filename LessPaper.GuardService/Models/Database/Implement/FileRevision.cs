using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using LessPaper.GuardService.Models.Database.Dtos;
using LessPaper.Shared.Interfaces.General;
using MongoDB.Driver.Core.Operations;

namespace LessPaper.GuardService.Models.Database.Implement
{
    public class FileRevision : IFileRevision
    {
        private readonly FileRevisionDto dto;

        public FileRevision(FileRevisionDto dto)
        {
            this.dto = dto;

            AccessKeys = dto.AccessKeys.ToDictionary(x => x.User.Id.AsString, x => (IAccessKey) new AccessKey(x));
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
