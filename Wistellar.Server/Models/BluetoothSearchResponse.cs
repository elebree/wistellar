namespace Wistellar.Server.Models
{
    using System.Collections.Generic;
    using System.Text.Json.Serialization;

    /// <summary>
    /// Response for Bluetooth search results.
    /// </summary>
    public class BluetoothSearchResponse
    {
        public bool Success { get; set; }
        public long TotalResults { get; set; }
        public long First { get; set; }
        public long Last { get; set; }
        public long ResultCount { get; set; }
        public List<BluetoothNetwork> Results { get; set; } = [];
        public string SearchAfter { get; set; } = "";
        [JsonPropertyName("search_after")]
        public long? SearchAfterDeprecated { get; set; }
    }

}
