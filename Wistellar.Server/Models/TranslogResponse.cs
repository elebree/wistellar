namespace Wistellar.Server.Models
{
    /// <summary>
    /// Model of the API upload list response
    /// </summary>
    public class TranslogResponse
    {
        public bool success { get; init; }
        public List<TransLog> results { get; init; } = [];
        public long processingQueueDepth { get; init; }
        public long geoQueueDepth { get; init; }
        public long trilaterationQueueDepth { get; init; }
    }
}
