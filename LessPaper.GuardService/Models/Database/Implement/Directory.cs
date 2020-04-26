using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using LessPaper.GuardService.Models.Database.Dtos;
using LessPaper.Shared.Enums;
using LessPaper.Shared.Interfaces.General;

namespace LessPaper.GuardService.Models.Database.Implement
{
    public class Directory : MinimalDirectoryMetadata, IDirectoryMetadata
    {
        private readonly DirectoryDto directoryDto;

        public Directory(DirectoryDto directoryDto, IFileMetadata[] fileChilds, IMinimalDirectoryMetadata[] directoryChilds) : base(directoryDto)
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
