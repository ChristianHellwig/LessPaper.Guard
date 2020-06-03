using LessPaper.Shared.Enums;
using LessPaper.Shared.Interfaces.General;

namespace LessPaper.Guard.Database.MongoDb.Models.Dtos
{
    public class FileDto : MetadataDto
    {
        
        public ExtensionType Extension { get; set; }
        
        public string ThumbnailId { get; set; }
        
        public string[] RevisionIds { get; set; }
        
        public ITag[] Tags { get; set; }
        
        public DocumentLanguage Language { get; set; }

        
    }
}
