
using System.Collections.Generic;

namespace LessPaper.GuardService.Models.Database.Dtos
{
    public class DirectoryDto : MetadataDto
    {
        public List<DirectoryDto> Directories { get; set; }

        public List<FileDto> Files { get; set; }

        

    }
}
