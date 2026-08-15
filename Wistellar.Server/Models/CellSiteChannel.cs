namespace Wistellar.Server.Models
{
    /// <summary>
    /// Represents a cell site channel.
    /// </summary>
    public class CellSiteChannel
    {
        public long Channel { get; set; }
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public int Qos { get; set; }
    }

}
