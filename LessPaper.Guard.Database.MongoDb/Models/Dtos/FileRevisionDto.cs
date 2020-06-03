using System;

namespace LessPaper.Guard.Database.MongoDb.Models.Dtos
{
    public class FileRevisionDto : BaseDto
    {
        public string File { get; set; }
        
        public uint SizeInBytes { get; set; }

        public DateTime ChangeDate { get; set; }

        public uint QuickNumber { get; set; }

        public AccessKeyDto[] AccessKeys { get; set; }

        public string OwnerId { get; set; }
    }
}
