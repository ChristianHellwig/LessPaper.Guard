namespace LessPaper.Guard.Database.MongoDb.Models.Dtos
{
    public class AccessKeyDto 
    {
        public string UserId { get; set; }
        
        public string IssuerId { get; set; }

        public string SymmetricEncryptedFileKey { get; set; }

    }
}
