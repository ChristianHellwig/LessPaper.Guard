using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using LessPaper.GuardService.Models.Database.Dtos;
using LessPaper.Shared.Interfaces.General;

namespace LessPaper.GuardService.Models.Database.Implement
{
    public class AccessKey : IAccessKey
    {
        private readonly AccessKeyDto dto;

        public AccessKey(AccessKeyDto dto)
        {
            this.dto = dto;
        }

        
        /// <inheritdoc />
        public string SymmetricEncryptedFileKey => dto.SymmetricEncryptedFileKey;

        /// <inheritdoc />
        public string IssuerId => dto.IssuerId;
    }
}
