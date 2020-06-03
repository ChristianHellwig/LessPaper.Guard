using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
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
        [JsonPropertyName("hashed_password")]
        [ModelBinder(Name = "hashed_password")]
        public string HashedPassword { get; set; }

        [Required]
        [JsonPropertyName("salt")]
        [ModelBinder(Name = "salt")]
        public string Salt { get; set; }

        [Required]
        [JsonPropertyName("user_id")]
        [ModelBinder(Name = "user_id")]
        public string UserId { get; set; }

        [Required]
        [JsonPropertyName("public_key")]
        [ModelBinder(Name = "public_key")]
        public string PublicKey { get; set; }


        [Required]
        [JsonPropertyName("encrypted_private_key")]
        [ModelBinder(Name = "encrypted_private_key")]
        public string EncryptedPrivateKey { get; set; }
    }
}
