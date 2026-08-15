using System.Text.Json.Serialization;

namespace Wistellar.Server.Models
{
    /// <summary>
    /// Represents the response from a WiGLE news API call.
    /// </summary>
    public class WiGLENews
    {
        /// <summary>
        /// Gets or sets a value indicating whether the API call was successful.
        /// </summary>
        [JsonPropertyName("success")]
        public bool Success { get; set; }

        /// <summary>
        /// Gets or sets the list of news items returned by the API.
        /// </summary>
        [JsonPropertyName("results")]
        public List<NewsItem> Results { get; set; } = [];

        /// <summary>
        /// Initializes a new instance of the <see cref="WiGLENews"/> class.
        /// </summary>
        public WiGLENews()
        {
        }
    }
}
