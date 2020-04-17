using System;
using System.Collections.Generic;
using LessPaper.Shared.Interfaces.General;

namespace LessPaper.GuardService.Models.Database.Dtos
{
    public class MetadataDto : BaseDto, IMetadata
    {
        public string ObjectName { get; set; }
        public string ObjectId { get; set; }
        public uint SizeInBytes { get; set; }
        public DateTime LatestChangeDate { get; set; }
        public DateTime LatestViewDate { get; set; }
        public List<PermissionDto> Permissions { get; set; }
    }
}
