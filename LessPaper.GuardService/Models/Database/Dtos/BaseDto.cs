using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Driver;

namespace LessPaper.GuardService.Models.Database.Dtos
{
    public class BaseDto
    {
        [BsonId]
        public string Id { get; set; }
    }
}
