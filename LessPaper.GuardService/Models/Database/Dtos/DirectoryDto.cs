
using System.Collections.Generic;
using LessPaper.Shared.Interfaces.General;
using MongoDB.Driver;

namespace LessPaper.GuardService.Models.Database.Dtos
{
    public class DirectoryDto : MinimalDirectoryMetadataDto
    {
        public List<MongoDBRef> Directories { get; set; }

        public List<MongoDBRef> Files { get; set; }

        public bool IsRootDirectory { get; set; }

        public string[] Path { get; set; }
    }
}
