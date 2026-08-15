namespace Wistellar.Server.Models
{
    using System.Collections.Generic;

    /// <summary>
    /// Response for channel details.
    /// </summary>
    public class ChannelDetailResponse
    {
        public bool Success { get; set; }
        public bool ResultsExceedLimit { get; set; }
        public List<CellSiteChannel> Results { get; set; } = [];
        public string ResultType { get; set; } = "";
    }

}
