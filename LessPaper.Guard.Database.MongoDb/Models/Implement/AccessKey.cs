using LessPaper.Guard.Database.MongoDb.Models.Dtos;
using LessPaper.Shared.Interfaces.General;

namespace LessPaper.Guard.Database.MongoDb.Models.Implement
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
