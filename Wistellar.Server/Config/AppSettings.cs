namespace Wistellar.Server.Config
{
    public class AppSettings
    {
        public string IssuerSigningKeyFilePath { get; set; } = "";

        public string ConnectionString { get; set; } = "";

        public ForwardedHeadersSettings ForwardedHeaders { get; set; } = new();
    }

    /// <summary>
    /// Controls whether X-Forwarded-* headers are honoured. This matters more than it looks:
    /// the rate limiter partitions by <c>Connection.RemoteIpAddress</c>, so behind a reverse
    /// proxy every client shares one bucket until the forwarded headers are applied.
    /// Off by default, because trusting these headers from an untrusted peer lets a caller
    /// spoof its own address.
    /// </summary>
    public class ForwardedHeadersSettings
    {
        public bool Enabled { get; set; }

        /// <summary>Individual proxy addresses to accept forwarded headers from, e.g. "172.18.0.2".</summary>
        public string[] KnownProxies { get; set; } = [];

        /// <summary>Proxy address ranges in CIDR notation, e.g. "172.16.0.0/12" for a Docker bridge network.</summary>
        public string[] KnownNetworks { get; set; } = [];

        /// <summary>How many proxy hops to walk back through. One proxy in front of the app means 1.</summary>
        public int ForwardLimit { get; set; } = 1;
    }
}
