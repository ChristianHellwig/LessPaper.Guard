using LessPaper.Guard.Database.MongoDb.Models.Dtos;
using LessPaper.Shared.Interfaces.General;

namespace LessPaper.Guard.Database.MongoDb.Models.Implement
{
    public class MinimalDirectoryMetadata : Metadata, IMinimalDirectoryMetadata
    {
        public MinimalDirectoryMetadata(MetadataDto dto, uint numberOfChilds) : base(dto)
        {
            NumberOfChilds = numberOfChilds;
        }
        
        /// <inheritdoc />
        public uint NumberOfChilds { get; }

    }
}