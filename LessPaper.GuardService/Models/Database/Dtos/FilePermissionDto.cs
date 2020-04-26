using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using LessPaper.Shared.Enums;
using MongoDB.Driver;

namespace LessPaper.GuardService.Models.Database.Dtos
{
    public class FilePermissionDto
    {
        public Permission Permission { get; set; }

        public MongoDBRef User { get; set; }

        public string EncryptedKey { get; set; }
    }
}
