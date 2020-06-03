using LessPaper.Guard.Database.MongoDb.Models.Dtos;
using LessPaper.Shared.Interfaces.General;

namespace LessPaper.Guard.Database.MongoDb.Models.Implement
{
    public class Directory : MinimalDirectoryMetadata, IDirectoryMetadata
    {
        private readonly DirectoryDto directoryDto;

        public Directory(DirectoryDto directoryDto, IFileMetadata[] fileChilds, IMinimalDirectoryMetadata[] directoryChilds) : base(directoryDto, (uint)(fileChilds.Length + directoryChilds.Length))
        {
            this.directoryDto = directoryDto;
            FileChilds = fileChilds;
            DirectoryChilds = directoryChilds;
        }

        /// <inheritdoc />
        public IFileMetadata[] FileChilds { get; }

        /// <inheritdoc />
        public IMinimalDirectoryMetadata[] DirectoryChilds { get; }

    }
}
