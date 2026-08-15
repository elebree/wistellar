using System.Text.Json.Serialization;

namespace Wistellar.Server.Models
{
    /// <summary>
    /// API Token response object
    /// </summary>
    public class ApiTokenResponse
    {
        [JsonPropertyName("success")]
        public bool Success { get; set; }
        [JsonPropertyName("authname")]
        public string AuthName { get; set; } = "";
        [JsonPropertyName("token")]
        public string Token { get; set; } = "";
    }
}
