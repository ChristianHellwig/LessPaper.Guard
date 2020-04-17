using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;

namespace LessPaper.GuardService.Models.Api
{
    public class UserCreationRequest : IdRequest
    {
        [Required]
        [JsonPropertyName("email")]
        [ModelBinder(Name = "email")]
        public string Email { get; set; }

        [Required]
        [JsonPropertyName("password")]
        [ModelBinder(Name = "password")]
        public string HashedPassword { get; set; }

        [Required]
        [JsonPropertyName("salt")]
        [ModelBinder(Name = "salt")]
        public string Salt { get; set; }
    }
}
