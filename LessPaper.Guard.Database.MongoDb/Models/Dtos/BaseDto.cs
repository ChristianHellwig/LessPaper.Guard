using MongoDB.Bson.Serialization.Attributes;

namespace LessPaper.Guard.Database.MongoDb.Models.Dtos
{
    public class BaseDto
    {
        [BsonId]
        public string Id { get; set; }
    }
}
