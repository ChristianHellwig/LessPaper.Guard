using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using LessPaper.GuardService.Models.Database.Dtos;
using LessPaper.Shared.Enums;
using LessPaper.Shared.Interfaces.General;

namespace LessPaper.GuardService.Models.Database.Implement
{
    public class Metadata : IMetadata
    {
        private readonly MetadataDto dto;

        public Metadata(MetadataDto dto)
        {
            this.dto = dto;

            Permissions = dto.Permissions.ToDictionary(
                x => x.User.Id.AsString,
                x => x.Permission);
        }

        /// <inheritdoc />
        public string ObjectName => dto.ObjectName;

        /// <inheritdoc />
        public string ObjectId => dto.Id;

        /// <inheritdoc />
        public Dictionary<string, Permission> Permissions { get; }
    }
}
