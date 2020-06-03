
using System.Collections.Generic;

namespace LessPaper.Guard.Database.MongoDb.Models.Dtos
{
    public class DirectoryDto : MetadataDto
    {
        public List<string> DirectoryIds { get; set; }

        public List<string> FileIds { get; set; }

        public bool IsRootDirectory { get; set; }

        public int ChangeDate { get; set; }
    }
}
