using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Wistellar.Core.Entities;

[Table("network")]
[Index(nameof(BestLongitude), nameof(BestLatitude), nameof(LastSeen), nameof(Type), nameof(SSID))]
public partial class Network
{
    [Key]
    [Column("bssid")]
    public string BSSID { get; set; } = null!;

    [Column("ssid")]
    public string SSID { get; set; } = null!;

    [Column("frequency")]
    public int Frequency { get; set; }


    [Column("capabilities")]
    public string Capabilities { get; set; } = null!;

    [Column("last_seen")]
    public long LastSeen { get; set; }

    [Column("last_lat")]
    public double Lastlatitude { get; set; }

    [Column("last_lon")]
    public double Lastlongitude { get; set; }

    public string Type { get; set; } = null!;

    [Column("best_level")]
    public int BestLevel { get; set; }

    [Column("best_lat")]
    public double BestLatitude { get; set; }

    [Column("last_lon")]
    public double BestLongitude { get; set; }

    // calculated fields
    [Column("range")]
    public long? Range { get; set; }

    [Column("dwell")]
    public long? Dwell { get; set; }

    [Column("observations")]
    public int? Observations { get; set; }
}
