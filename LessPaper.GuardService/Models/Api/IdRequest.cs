using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc;

namespace LessPaper.GuardService.Models.Api
{
    public class IdRequest
    {
        [Required]
        [JsonPropertyName("object_id")]
        [ModelBinder(Name = "object_id")]
        public string Id { get; set; }
    }
}
