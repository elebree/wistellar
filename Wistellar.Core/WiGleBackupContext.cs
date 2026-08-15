using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using wistellar.core.Conventions;
using Wistellar.Core.Entities;
using Wistellar.Core.Models;

namespace Wistellar.Core;

public partial class WiGleBackupContext : DbContext
{
    private readonly string connectionString = "";
    private readonly ILogger? logger = null;

    /// <summary>
    /// Parameterless constructor used by the EF Core design-time tools, so migrations can be
    /// created against this project without building the server.
    /// </summary>
    public WiGleBackupContext()
    {
    }

    public WiGleBackupContext(string connectionString, ILoggerFactory? loggerFactory = null)
    {
        logger = loggerFactory?.CreateLogger<WiGleBackupContext>();
        this.connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
    }

    public WiGleBackupContext(DbContextOptions<WiGleBackupContext> options)
        : base(options)
    {
    }

    public virtual DbSet<AndroidMetadatum> AndroidMetadata { get; set; }

    public virtual DbSet<Location> Locations { get; set; }

    public virtual DbSet<Network> Networks { get; set; }

    public virtual DbSet<Route> Routes { get; set; }

    public virtual DbSet<Observation> Observations { get; set; }

    public virtual DbSet<OUI> Oui { get; set; }

    public virtual DbSet<MccMnc> MccMnc { get; set; }

    public virtual DbSet<WsUser> Users { get; set; }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        // connectionString is a bare file path, not a full connection string.
        optionsBuilder.UseSqlite($"Data Source={connectionString}");
    }

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        configurationBuilder.Conventions.Add(sp => new TimestampConvention());
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<AndroidMetadatum>(entity =>
        {
            entity
                .HasNoKey()
                .ToTable("android_metadata");

            entity.Property(e => e.Locale).HasColumnName("locale");
        });

        modelBuilder.Entity<Location>(entity =>
        {
            entity.ToTable("location");
            entity.HasIndex(e => new { e.Bssid });
            entity.Property(e => e.Id).HasColumnName("_id");
            entity.Property(e => e.Accuracy)
                .HasColumnType("float")
                .HasColumnName("accuracy");
            entity.Property(e => e.Altitude)
                .HasColumnType("double")
                .HasColumnName("altitude");
            entity.Property(e => e.Bssid).HasColumnName("bssid");
            entity.Property(e => e.User).HasColumnName("external");
            entity.Property(e => e.Lat)
                .HasColumnType("double")
                .HasColumnName("lat");
            entity.Property(e => e.Level).HasColumnName("level");
            entity.Property(e => e.Lon)
                .HasColumnType("double")
                .HasColumnName("lon");
            entity.Property(e => e.Time)
                .HasColumnType("long")
                .HasColumnName("time");
        });

        modelBuilder.Entity<Network>(entity =>
        {
            entity.HasKey(e => e.BSSID);

            entity.ToTable("network");

            entity.Property(e => e.BSSID).HasColumnName("bssid");
            entity.Property(e => e.BestLatitude)
                .HasColumnType("double")
                .HasColumnName("bestlat");
            entity.Property(e => e.BestLevel).HasColumnName("bestlevel");
            entity.Property(e => e.BestLongitude)
                .HasColumnType("double")
                .HasColumnName("bestlon");
            entity.Property(e => e.Capabilities).HasColumnName("capabilities");
            entity.Property(e => e.Frequency)
                .HasColumnType("INT")
                .HasColumnName("frequency");
            entity.Property(e => e.Lastlatitude)
                .HasColumnType("double")
                .HasColumnName("lastlat");
            entity.Property(e => e.Lastlongitude)
                .HasColumnType("double")
                .HasColumnName("lastlon");
            entity.Property(e => e.LastSeen)
                .HasColumnType("long")
                .HasColumnName("lasttime");
            entity.Property(e => e.SSID).HasColumnName("ssid");
            entity.Property(e => e.Type)
                .HasDefaultValue("W")
                .HasColumnName("type");
        });

        modelBuilder.Entity<Route>(entity =>
        {
            entity.ToTable("route");

            entity.Property(e => e.Id).HasColumnName("_id");
            entity.Property(e => e.Accuracy)
                .HasColumnType("float")
                .HasColumnName("accuracy");
            entity.Property(e => e.Altitude)
                .HasColumnType("double")
                .HasColumnName("altitude");
            entity.Property(e => e.BtVisible).HasColumnName("bt_visible");
            entity.Property(e => e.CellVisible).HasColumnName("cell_visible");
            entity.Property(e => e.Lat)
                .HasColumnType("double")
                .HasColumnName("lat");
            entity.Property(e => e.Lon)
                .HasColumnType("double")
                .HasColumnName("lon");
            entity.Property(e => e.RunId).HasColumnName("run_id");
            entity.Property(e => e.Time)
                .HasColumnType("long")
                .HasColumnName("time");
            entity.Property(e => e.WifiVisible).HasColumnName("wifi_visible");
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
