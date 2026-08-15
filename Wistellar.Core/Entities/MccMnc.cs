using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Wistellar.Core.Entities
{
    // Define a class to hold MccMnc record details.
    [Table("mccmnc")]
    [Index(nameof(MCC), nameof(MNC), IsUnique = true)]
    public class MccMnc
    {
        [Column("mcc")]
        [Required]
        public int MCC { get; set; }
        [Column("mnc")]
        [Required]
        public int MNC { get; set; }
        [Column("plmn")]
        [Required]
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.None)]
        public int PLMN { get; set; }
        [Column("region")]
        [Required]
        public string Region { get; set; } = "";
        [Column("country")]
        [Required]
        public string Country { get; set; } = "";
        [Column("iso")]
        [Required]
        public string ISO { get; set; } = "";
        [Column("operator")]
        public string? Operator { get; set; } = null;
        [Column("brand")]
        public string? Brand { get; set; } = null;
        [Column("tadig")]
        public string? TADIG { get; set; } = null;
        [Column("bands")]
        public string? Bands { get; set; } = null;
    }
}
