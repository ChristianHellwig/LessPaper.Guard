using LessPaper.Shared.Enums;
using LessPaper.Shared.Interfaces.General;

namespace LessPaper.Guard.Database.MongoDb.Models.Dtos
{
    public class TagDto : ITag
    {
        public string Value { get; set; }
        public float Relevance { get; set; }
        public TagSource Source { get; set; }
    }
}
