using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Wistellar.Core.Entities
{
    // Define a class to hold OUI record details.
    [Table("oui")]
    public class OUI
    {
        [Key]
        [Column("mac")]
        public string MacAddress { get; set; } = "";

        [Column("base16")]
        public string Base16 { get; set; } = "";

        [Column("organisation")]
        public string Organization { get; set; } = "";

        [Column("address")]
        public string? Address { get; set; }

        [Column("city")]
        public string? City { get; set; }

        [Column("country")]
        public string? Country { get; set; }
    }
}
