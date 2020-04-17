using System;
using LessPaper.Shared.Enums;
using LessPaper.Shared.Interfaces.General;

namespace LessPaper.GuardService.Models.Database.Dtos
{
    public class FileDto : MetadataDto, IFileMetadata
    {
        public uint QuickNumber { get; set; }
        public ExtensionType Extension { get; set; }

        public DateTime UploadDate { get; set; }
        public string EncryptionKey { get; set; }
        public uint RevisionNumber { get; set; }
        public string Hash { get; set; }
        public string ThumbnailId { get; set; }
        public uint[] Revisions { get; set; }
        public string[] ParentDirectoryIds { get; set; }
        public TagDto[] Tags { get; set; }
        public DocumentLanguage Language { get; set; }

        ITag[] IFileMetadata.Tags => Tags;
    }
}
