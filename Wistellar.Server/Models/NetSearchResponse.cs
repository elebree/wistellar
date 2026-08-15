using System.Text.Json.Serialization;

namespace Wistellar.Server.Models
{
    /// <summary>
    /// Response for network search results.
    /// </summary>
    public class NetSearchResponse
    {
        public bool Success { get; set; }
        public long TotalResults { get; set; }
        public long First { get; set; }
        public long Last { get; set; }
        public long ResultCount { get; set; }
        public string SearchAfter { get; set; } = "";
        [JsonPropertyName("search_after")]
        public long? SearchAfterDeprecated { get; set; }
    }

}
