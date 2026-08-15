namespace Wistellar.Server.Models
{
    /// <summary>
    /// API response for uploads
    /// </summary>
    public class UploadResponse
    {
        public bool Success { get; set; }
        public string Warning { get; set; } = "";
        public UploadResultsResponse Results { get; set; } = new();
        public string Observer { get; set; } = "";
    }
}
