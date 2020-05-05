using System;
using LessPaper.Shared.Enums;
using LessPaper.Shared.Interfaces.General;
using MongoDB.Bson.Serialization.Attributes;

namespace LessPaper.GuardService.Models.Database.Dtos
{
    public class FileDto : MetadataDto
    {
        public uint QuickNumber { get; set; }
        
        public ExtensionType Extension { get; set; }
        
        public string ThumbnailId { get; set; }
        
        public FileRevisionDto[] Revisions { get; set; }
        
        public ITag[] Tags { get; set; }
        
        public DocumentLanguage Language { get; set; }
        
    }
}
