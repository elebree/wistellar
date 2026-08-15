namespace Wistellar.Core.Models
{
    /// <summary>
    /// A single record of the IEEE OUI registry, mapping a MAC address prefix to its vendor.
    /// </summary>
    public class OuiVendorInfo
    {
        public string MacAddress { get; set; } = "";
        public string Base16 { get; set; } = "";
        public string Organization { get; set; } = "";
        public string? Address { get; set; }
        public string? City { get; set; }
        public string? Country { get; set; }
    }
}
