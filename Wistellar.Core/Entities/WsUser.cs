namespace Wistellar.Core.Entities
{
    using Microsoft.EntityFrameworkCore;
    using System;
    using System.ComponentModel.DataAnnotations;
    using System.ComponentModel.DataAnnotations.Schema;
    using System.Runtime.Serialization;

    // Create indexes for UUID and Username (both must be unique)
    [Table("ws_users")]
    [Index(nameof(Uuid), IsUnique = true)]
    [Index(nameof(Username), IsUnique = true)]

    public class WsUser
    {
        // Primary key with auto-increment
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        [Column("id")]
        public int Id { get; set; }

        // UUID generated in code
        [Required]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        [Column("uuid")]
        public string Uuid { get; set; } = Guid.NewGuid().ToString();

        // Secret (e.g., password hash or API key) - required
        [Required]
        [Column("secret")]
        public string Secret { get; set; } = "";

        // Unique username - required
        [Required]
        [Column("username")]
        public string Username { get; set; } = "";

        // Role (must be one of: member, moderator, admin, contributor)
        [Required]
        [Column("role")]
        [EnumDataType(typeof(UserRole))]
        public UserRole Role { get; set; } = UserRole.Member;

        // Active status - required, default true (1)
        [Required]
        [Column("active")]
        public bool Active { get; set; } = true;

        // Created timestamp - required, default is current UTC datetime
        [Required]
        [Column("created")]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public DateTime Created { get; set; } = DateTime.UtcNow;

        // Updated timestamp - required, default is current UTC datetime
        [Required]
        [Column("updated")]
        [DatabaseGenerated(DatabaseGeneratedOption.Computed)]
        public DateTime Updated { get; set; } = DateTime.UtcNow;
    }

    // Enum for role values to enforce allowed options
    public enum UserRole
    {
        [EnumMember(Value = "member")]
        Member,
        [EnumMember(Value = "moderator")]
        Moderator,
        [EnumMember(Value = "contributor")]
        Contributor,
        [EnumMember(Value = "admin")]
        Admin
    }
}
