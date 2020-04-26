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
        }

        /// <inheritdoc />
        public uint RevisionNumber => dto.RevisionNumber;

        /// <inheritdoc />
        public uint SizeInBytes => dto.SizeInBytes;

        /// <inheritdoc />
        public DateTime ChangeDate => dto.ChangeDate;

        /// <inheritdoc />
        public string BlobId => dto.BlobId;
    }
}
