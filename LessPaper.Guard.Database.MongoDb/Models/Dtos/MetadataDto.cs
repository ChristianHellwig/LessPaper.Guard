namespace LessPaper.Guard.Database.MongoDb.Models.Dtos
{
    public class MetadataDto : BaseDto
    {
        public string ObjectName { get; set; }
        
        public string OwnerId { get; set; }

        public BasicPermissionDto[] Permissions { get; set; }

        public string[] PathIds { get; set; }

        public string ParentDirectoryId { get; set; }


    }
}
