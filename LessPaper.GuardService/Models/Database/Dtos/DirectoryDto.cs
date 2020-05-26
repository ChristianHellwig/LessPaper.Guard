
using System.Collections.Generic;
using LessPaper.Shared.Interfaces.General;
using MongoDB.Driver;

namespace LessPaper.GuardService.Models.Database.Dtos
{
    public class DirectoryDto : MetadataDto
    {
        public List<string> DirectoryIds { get; set; }

        public List<string> FileIds { get; set; }

        public bool IsRootDirectory { get; set; }

    }
}
