using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Wistellar.Core.Models
{
    /// <summary>
    /// MAC,SSID,AuthMode,FirstSeen,Channel,Frequency,RSSI,CurrentLatitude,CurrentLongitude,AltitudeMeters,AccuracyMeters,RCOIs,MfgrId,Type
    /// </summary>
    [Table("observation")]
    public class Observation
    {
        [Key]
        public int Id { get; set; }
        [Required]
        public string MAC { get; set; } = string.Empty;
        public string SSID { get; set; } = string.Empty;
        [Required]
        public string AuthMode { get; set; } = string.Empty;
        [Required]
        public DateTime FirstSeen { get; set; }
        public int? Channel { get; set; }
        public int? Frequency { get; set; }
        [Required]
        public int RSSI { get; set; }
        [Required]
        public double CurrentLatitude { get; set; }
        [Required]
        public double CurrentLongitude { get; set; }
        [Required]
        public double AltitudeMeters { get; set; }
        [Required]
        public double AccuracyMeters { get; set; }
        public string? RCOIs { get; set; }
        public string? MfgrId { get; set; }

        [Required]
        public string Type { get; set; } = string.Empty;

        public override string ToString()
        {
            return MAC + "" + SSID;
        }
    }
}
