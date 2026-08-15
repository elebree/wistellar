using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Wistellar.Core.Entities;

/// <summary>
/// Represents a location record in the database.
/// </summary>
[Table("location")]
[Index(nameof(Bssid))]
[Index(nameof(Time), nameof(Bssid), IsUnique = true)]
public partial class Location
{
    /// <summary>
    /// Unique identifier for the location.
    /// </summary>
    [Key]
    [Column("id")]
    public long Id { get; set; }

    /// <summary>
    /// BSSID (Basic Service Set Identifier) of the network device.
    /// </summary>
    [Column("bssid")]
    public string Bssid { get; set; } = null!;

    /// <summary>
    /// Signal level of the network device.
    /// </summary>
    [Column("level")]
    public int Level { get; set; }

    /// <summary>
    /// Latitude coordinate of the location.
    /// </summary>
    [Column("lat")]
    public double Lat { get; set; }

    /// <summary>
    /// Longitude coordinate of the location.
    /// </summary>
    [Column("lon")]
    public double Lon { get; set; }

    /// <summary>
    /// Altitude of the location.
    /// </summary>
    [Column("altitude")]
    public double Altitude { get; set; }

    /// <summary>
    /// Accuracy of the location data.
    /// </summary>
    [Column("accuracy")]
    public double Accuracy { get; set; }

    /// <summary>
    /// Time when the location data was recorded.
    /// </summary>
    [Column("time")]
    public long Time { get; set; }

    /// <summary>
    /// User identifier associated with the location data.
    /// </summary>
    [Column("user")]
    public int User { get; set; }

    /// <summary>
    /// Transaction identifier associated with the location data.
    /// </summary>
    [Column("transaction")]
    public int Transaction { get; set; }

    /// <summary>
    /// Network associated with this location.
    /// </summary>
    [ForeignKey(nameof(Bssid))]
    //[NotMapped]
    public Network? Network { get; set; }
}
