using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Xml;
using LessPaper.GuardService.Models.Database.Dtos;
using LessPaper.Shared.Enums;
using LessPaper.Shared.Interfaces.General;

namespace LessPaper.GuardService.Models.Database.Implement
{
    public class File : Metadata, IFileMetadata
    {
        private readonly FileDto dto;

        /// <inheritdoc />
        public File(FileDto dto, FileRevisionDto[] revisions) : base(dto)
        {
            this.dto = dto;
            Revisions = revisions
                .Select(x => new FileRevision(x))
                .Cast<IFileRevision>()
                .ToArray();

        }
        
        /// <inheritdoc />
        public ExtensionType Extension => dto.Extension;
        
        /// <inheritdoc />
        public string ThumbnailId => dto.ThumbnailId;

        /// <inheritdoc />
        public IFileRevision[] Revisions { get; }
        
        /// <inheritdoc />
        public ITag[] Tags => dto.Tags;

        /// <inheritdoc />
        public DocumentLanguage Language => dto.Language;

    }
}
