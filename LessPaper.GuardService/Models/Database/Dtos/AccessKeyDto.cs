using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using LessPaper.Shared.Enums;
using LessPaper.Shared.Interfaces.General;
using MongoDB.Driver;

namespace LessPaper.GuardService.Models.Database.Dtos
{
    public class AccessKeyDto 
    {
        public string UserId { get; set; }
        
        public string IssuerId { get; set; }

        public string SymmetricEncryptedFileKey { get; set; }

    }
}
