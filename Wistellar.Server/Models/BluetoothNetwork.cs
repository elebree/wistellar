namespace Wistellar.Server.Models
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Represents a Bluetooth network.
    /// </summary>
    public class BluetoothNetwork
    {
        public double Trilat { get; set; }
        public double Trilong { get; set; }
        public string Ssid { get; set; } = "";
        public int Qos { get; set; }
        public string Transid { get; set; } = "";
        public DateTime Firsttime { get; set; }
        public DateTime Lasttime { get; set; }
        public DateTime Lastupdt { get; set; }
        public string Netid { get; set; } = "";
        public string Type { get; set; } = "";
        public List<string> Capabilities { get; set; } = [];
        public bool Userfound { get; set; }
        public int Device { get; set; }
        public long MfgrId { get; set; }
        public string Name { get; set; } = "";
        public string Country { get; set; } = "";
        public string Region { get; set; } = "";
        public string Road { get; set; } = "";
        public string City { get; set; } = "";
        public string Housenumber { get; set; } = "";
        public string Postalcode { get; set; } = "";
    }

}
