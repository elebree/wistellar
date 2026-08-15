using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Wistellar.Core.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "android_metadata",
                columns: table => new
                {
                    locale = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                });

            migrationBuilder.CreateTable(
                name: "mccmnc",
                columns: table => new
                {
                    plmn = table.Column<int>(type: "INTEGER", nullable: false),
                    mcc = table.Column<int>(type: "INTEGER", nullable: false),
                    mnc = table.Column<int>(type: "INTEGER", nullable: false),
                    region = table.Column<string>(type: "TEXT", nullable: false),
                    country = table.Column<string>(type: "TEXT", nullable: false),
                    iso = table.Column<string>(type: "TEXT", nullable: false),
                    @operator = table.Column<string>(name: "operator", type: "TEXT", nullable: true),
                    brand = table.Column<string>(type: "TEXT", nullable: true),
                    tadig = table.Column<string>(type: "TEXT", nullable: true),
                    bands = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_mccmnc", x => x.plmn);
                });

            migrationBuilder.CreateTable(
                name: "network",
                columns: table => new
                {
                    bssid = table.Column<string>(type: "TEXT", nullable: false),
                    ssid = table.Column<string>(type: "TEXT", nullable: false),
                    frequency = table.Column<int>(type: "INT", nullable: false),
                    capabilities = table.Column<string>(type: "TEXT", nullable: false),
                    lasttime = table.Column<long>(type: "long", nullable: false),
                    lastlat = table.Column<double>(type: "double", nullable: false),
                    lastlon = table.Column<double>(type: "double", nullable: false),
                    type = table.Column<string>(type: "TEXT", nullable: false, defaultValue: "W"),
                    bestlevel = table.Column<int>(type: "INTEGER", nullable: false),
                    bestlat = table.Column<double>(type: "double", nullable: false),
                    bestlon = table.Column<double>(type: "double", nullable: false),
                    range = table.Column<long>(type: "INTEGER", nullable: true),
                    dwell = table.Column<long>(type: "INTEGER", nullable: true),
                    observations = table.Column<int>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_network", x => x.bssid);
                });

            migrationBuilder.CreateTable(
                name: "observation",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    MAC = table.Column<string>(type: "TEXT", nullable: false),
                    SSID = table.Column<string>(type: "TEXT", nullable: false),
                    AuthMode = table.Column<string>(type: "TEXT", nullable: false),
                    FirstSeen = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Channel = table.Column<int>(type: "INTEGER", nullable: true),
                    Frequency = table.Column<int>(type: "INTEGER", nullable: true),
                    RSSI = table.Column<int>(type: "INTEGER", nullable: false),
                    CurrentLatitude = table.Column<double>(type: "REAL", nullable: false),
                    CurrentLongitude = table.Column<double>(type: "REAL", nullable: false),
                    AltitudeMeters = table.Column<double>(type: "REAL", nullable: false),
                    AccuracyMeters = table.Column<double>(type: "REAL", nullable: false),
                    RCOIs = table.Column<string>(type: "TEXT", nullable: true),
                    MfgrId = table.Column<string>(type: "TEXT", nullable: true),
                    Type = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_observation", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "oui",
                columns: table => new
                {
                    mac = table.Column<string>(type: "TEXT", nullable: false),
                    base16 = table.Column<string>(type: "TEXT", nullable: false),
                    organisation = table.Column<string>(type: "TEXT", nullable: false),
                    address = table.Column<string>(type: "TEXT", nullable: true),
                    city = table.Column<string>(type: "TEXT", nullable: true),
                    country = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_oui", x => x.mac);
                });

            migrationBuilder.CreateTable(
                name: "route",
                columns: table => new
                {
                    _id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    run_id = table.Column<int>(type: "INTEGER", nullable: false),
                    wifi_visible = table.Column<int>(type: "INTEGER", nullable: false),
                    cell_visible = table.Column<int>(type: "INTEGER", nullable: false),
                    bt_visible = table.Column<int>(type: "INTEGER", nullable: false),
                    lat = table.Column<double>(type: "double", nullable: false),
                    lon = table.Column<double>(type: "double", nullable: false),
                    altitude = table.Column<double>(type: "double", nullable: false),
                    accuracy = table.Column<double>(type: "float", nullable: false),
                    time = table.Column<byte[]>(type: "long", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_route", x => x._id);
                });

            migrationBuilder.CreateTable(
                name: "ws_users",
                columns: table => new
                {
                    id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    uuid = table.Column<string>(type: "TEXT", nullable: false),
                    secret = table.Column<string>(type: "TEXT", nullable: false),
                    username = table.Column<string>(type: "TEXT", nullable: false),
                    role = table.Column<int>(type: "INTEGER", nullable: false),
                    active = table.Column<bool>(type: "INTEGER", nullable: false),
                    created = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    updated = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ws_users", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "location",
                columns: table => new
                {
                    _id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    bssid = table.Column<string>(type: "TEXT", nullable: false),
                    level = table.Column<int>(type: "INTEGER", nullable: false),
                    lat = table.Column<double>(type: "double", nullable: false),
                    lon = table.Column<double>(type: "double", nullable: false),
                    altitude = table.Column<double>(type: "double", nullable: false),
                    accuracy = table.Column<double>(type: "float", nullable: false),
                    time = table.Column<long>(type: "long", nullable: false),
                    external = table.Column<int>(type: "INTEGER", nullable: false),
                    // The import pipeline inserts locations without naming this column, so it must
                    // carry a default.
                    transaction = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 0)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_location", x => x._id);
                    // Deliberately no foreign key from location.bssid to network.bssid. The model
                    // infers that relationship from the Location.Network navigation property, but
                    // no deployed database has ever carried the constraint, and enforcing it would
                    // break imports: locations are written before the networks they reference.
                });

            migrationBuilder.CreateIndex(
                name: "IX_location_bssid",
                table: "location",
                column: "bssid");

            migrationBuilder.CreateIndex(
                name: "IX_location_time_bssid",
                table: "location",
                columns: new[] { "time", "bssid" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_mccmnc_mcc_mnc",
                table: "mccmnc",
                columns: new[] { "mcc", "mnc" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_network_bestlon_bestlat_lasttime_type_ssid",
                table: "network",
                columns: new[] { "bestlon", "bestlat", "lasttime", "type", "ssid" });

            migrationBuilder.CreateIndex(
                name: "IX_ws_users_username",
                table: "ws_users",
                column: "username",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ws_users_uuid",
                table: "ws_users",
                column: "uuid",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "android_metadata");

            migrationBuilder.DropTable(
                name: "location");

            migrationBuilder.DropTable(
                name: "mccmnc");

            migrationBuilder.DropTable(
                name: "observation");

            migrationBuilder.DropTable(
                name: "oui");

            migrationBuilder.DropTable(
                name: "route");

            migrationBuilder.DropTable(
                name: "ws_users");

            migrationBuilder.DropTable(
                name: "network");
        }
    }
}
