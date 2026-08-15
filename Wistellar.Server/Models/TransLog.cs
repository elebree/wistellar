namespace Wistellar.Server.Models
{
    /// <summary>
    /// Represents a transaction log.
    /// </summary>
    public class TransLog
    {
        public string Transid { get; set; } = "";
        public string Username { get; set; } = "";
        public DateTime FirstTime { get; set; }
        public DateTime Lastupdt { get; set; }
        public string FileName { get; set; } = "";
        public long FileSize { get; set; }
        public long FileLines { get; set; }
        public string Status { get; set; } = "";
        public long DiscoveredGps { get; set; }
        public long Discovered { get; set; }
        public long Total { get; set; }
        public long TotalGps { get; set; }
        public long TotalLocations { get; set; }
        public float PercentDone { get; set; }
        public long TimeParsing { get; set; }
        public long GenDiscovered { get; set; }
        public long GenDiscoveredGps { get; set; }
        public long GenTotal { get; set; }
        public long GenTotalGps { get; set; }
        public long GenTotalLocations { get; set; }
        public long BtDiscovered { get; set; }
        public long BtDiscoveredGps { get; set; }
        public long BtTotal { get; set; }
        public long BtTotalGps { get; set; }
        public long BtTotalLocations { get; set; }
        public string WwwdStatus { get; set; } = "";
        public long Wait { get; set; }
        public string Brand { get; set; } = "";
        public string Model { get; set; } = "";
        public string OsRelease { get; set; } = "";
    }
}
