namespace Wistellar.Server.Models
{
    using System;

    /// <summary>
    /// Represents a WiFi network.
    /// </summary>
    public class WiFiNetwork
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
        public string Name { get; set; } = "";
        public string Type { get; set; } = "WiFi";
        public string Comment { get; set; } = "";
        public string Wep { get; set; } = "";
        public int Bcninterval { get; set; }
        public string Freenet { get; set; } = "";
        public string Dhcp { get; set; } = "";
        public string Paynet { get; set; } = "";
        public bool Userfound { get; set; }
        public int Channel { get; set; }
        public int Frequency { get; set; }
        public string Rcois { get; set; } = "";
        public string Encryption { get; set; } = "";
        public string Country { get; set; } = "";
        public string Region { get; set; } = "";
        public string Road { get; set; } = "";
        public string City { get; set; } = "";
        public string Housenumber { get; set; } = "";
        public string Postalcode { get; set; } = "";
    }

}
