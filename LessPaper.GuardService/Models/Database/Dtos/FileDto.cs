using System;
using System.Collections.Generic;
using LessPaper.Shared.Enums;
using LessPaper.Shared.Interfaces.General;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Driver;

namespace LessPaper.GuardService.Models.Database.Dtos
{
    public class FileDto : MetadataDto
    {
        
        public ExtensionType Extension { get; set; }
        
        public string ThumbnailId { get; set; }
        
        public MongoDBRef[] Revisions { get; set; }
        
        public ITag[] Tags { get; set; }
        
        public DocumentLanguage Language { get; set; }

        public MongoDBRef ParentDirectory { get; set; }
        
    }
}
