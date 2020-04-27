using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using LessPaper.Shared.Enums;
using LessPaper.Shared.Interfaces.General;

namespace LessPaper.GuardService.Models.Database.Dtos
{
    public class MinimalDirectoryMetadataDto : MetadataDto
    {
        public string ObjectId { get; set; }

        public uint NumberOfChilds { get; set; }

        public BasicPermissionDto[] Permissions { get; set; }
    }
}
