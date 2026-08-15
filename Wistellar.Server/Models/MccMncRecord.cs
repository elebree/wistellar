namespace Wistellar.Server.Models
{
    /// <summary>
    /// Represents an MCC/MNC record.
    /// </summary>
    public class MccMncRecord
    {
        public string Type { get; set; } = "";
        public string CountryName { get; set; } = "";
        public string CountryCode { get; set; } = "";
        public string Mcc { get; set; } = "";
        public string Mnc { get; set; } = "";
        public string Brand { get; set; } = "";
        public string Operator { get; set; } = "";
        public string Status { get; set; } = "";
        public string Bands { get; set; } = "";
        public string Notes { get; set; } = "";
    }

}
