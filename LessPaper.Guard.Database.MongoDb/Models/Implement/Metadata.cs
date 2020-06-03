using System.Collections.Generic;
using System.Linq;
using LessPaper.Guard.Database.MongoDb.Models.Dtos;
using LessPaper.Shared.Enums;
using LessPaper.Shared.Interfaces.General;

namespace LessPaper.Guard.Database.MongoDb.Models.Implement
{
    public class Metadata : IMetadata
    {
        private readonly MetadataDto dto;

        public Metadata(MetadataDto dto)
        {
            this.dto = dto;

            Permissions = dto.Permissions.ToDictionary(
                x => x.UserId,
                x => x.Permission);

            Path = dto.PathIds.Aggregate("", (current, next) => current + "/" + next);
        }

        /// <inheritdoc />
        public string ObjectName => dto.ObjectName;

        /// <inheritdoc />
        public string ObjectId => dto.Id;

        /// <inheritdoc />
        public Dictionary<string, Permission> Permissions { get; }

        /// <inheritdoc />
        public string Path { get; }
    }
}
