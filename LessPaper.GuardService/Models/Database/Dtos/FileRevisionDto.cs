using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using LessPaper.Shared.Interfaces.General;

namespace LessPaper.GuardService.Models.Database.Dtos
{
    public class FileRevisionDto 
    {
        public uint RevisionNumber { get; set; }

        public uint SizeInBytes { get; set; }

        public DateTime ChangeDate { get; set; }

        public string BlobId { get; set; }

        public AccessKeyDto[] AccessKeys { get; set; }
    }
}
