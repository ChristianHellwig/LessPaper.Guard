using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using LessPaper.Shared.Interfaces.General;
using MongoDB.Driver;

namespace LessPaper.GuardService.Models.Database.Dtos
{
    public class FileRevisionDto : BaseDto
    {
        public MongoDBRef File { get; set; }
        
        public uint SizeInBytes { get; set; }

        public DateTime ChangeDate { get; set; }

        public uint QuickNumber { get; set; }

        public AccessKeyDto[] AccessKeys { get; set; }

        public MongoDBRef Owner { get; set; }
    }
}
