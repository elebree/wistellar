using EFCore.BulkExtensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Runtime.CompilerServices;
using Wistellar.Core.Entities;
using Wistellar.Core.Models;
using Wistellar.ExpressionVisitors;

namespace Wistellar.Core.Services
{
    /// <summary>
    /// Import and query operations over the observation database. Schema creation is not handled
    /// here: migrations are applied by the <see cref="WiGleBackupContext"/> factory in the server's
    /// service configuration.
    /// </summary>
    public class DatabaseService
    {
        private ILogger? logger;
        private WiGleBackupContext context;
        public DatabaseService(ILoggerFactory loggerFactory, WiGleBackupContext context)
        {
            logger = loggerFactory?.CreateLogger<DatabaseService>();
            this.context = context;
        }

        /// <summary>
        /// Recomputes the columns that are derived from <c>location</c> rather than maintained
        /// incrementally: observation count, dwell time and estimated range.
        /// </summary>
        public async Task UpdateCalculatedColumns()
        {
            logger?.LogInformation("Update calculated columns");
            await context.Database.ExecuteSqlAsync($@"UPDATE network
SET
    observations = loc.observations,
    dwell = loc.duration,
    range = loc.distance
FROM
    (
        SELECT
            location.bssid as bssid,
            COUNT(*) as observations,
            (MAX(time) - MIN(time)) / 1000 as duration,
            cast(
                2 * 6335 * 1000 * asin(
                    sqrt(
                        pow (
                            sin((radians (max(lat)) - radians (min(lat))) / 2),
                            2
                        ) + cos(radians (min(lat))) * cos(radians (max(lat))) * pow (
                            sin((radians (max(lon)) - radians (min(lon))) / 2),
                            2
                        )
                    )
                ) AS INT
            ) as distance
        FROM
            network
            left join location
        where
            network.bssid = location.bssid
            and ""time"" <> 0
        group by
            network.bssid
    ) as loc
WHERE
    network.bssid = loc.bssid;");
            await context.SaveChangesAsync();
            logger?.LogInformation("Update calculated columns done");
        }

        /// <summary>
        /// Bulk-loads observations into the <c>observation</c> scratch table, then promotes them
        /// into <c>location</c> and <c>network</c>.
        /// </summary>
        public async Task AddObservations(IEnumerable<Observation> observations)
        {
            logger?.LogInformation("Import observations");

            using var transaction = await context.Database.BeginTransactionAsync();

            // observation is a scratch table: only the rows from the current import belong in it.
            await context.Database.ExecuteSqlAsync($"DELETE FROM observation;");

            var config = new BulkConfig()
            {
                CustomDestinationTableName = "observation",
                UseTempDB = true,
                UniqueTableNameTempDb = true,
            };

            logger?.LogInformation("Bulk insert observations...");
            context.BulkInsert(observations, config);

            logger?.LogInformation("Import locations...");
            await ImportLocationsFromObservationsTable();

            logger?.LogInformation("Import networks...");
            await ImportNetworksFromObservationsTable();

            await transaction.CommitAsync();
            await context.SaveChangesAsync();

            logger?.LogInformation("Import observations done");
        }

        private async Task ImportLocationsFromObservationsTable()
        {
            await context.Database.ExecuteSqlAsync($@"INSERT OR IGNORE INTO location (bssid, level, lat, lon, altitude, accuracy, time, external)
SELECT 
    MAC,
    RSSI,
	CurrentLatitude,
	CurrentLongitude,
    AltitudeMeters, 
    AccuracyMeters, 
    strftime('%s', FirstSeen) * 1000,
    false
FROM observation WHERE FirstSeen != '1970-01-01 00:00:00';");
        }

        private async Task ImportNetworksFromObservationsTable()
        {
            await context.Database.ExecuteSqlAsync($@"INSERT INTO network (bssid, ssid, frequency, capabilities, lasttime, lastlat, lastlon, type, bestlevel, bestlat, bestlon)
SELECT 
    MAC, 
    SSID, 
    COALESCE(Frequency, 0), 
    AuthMode, 
    strftime('%s', FirstSeen) * 1000,
    CurrentLatitude, 
    CurrentLongitude, 
    CASE 
        WHEN Type = 'WIFI'  THEN 'W'
        WHEN Type = 'GSM'   THEN 'G'
		WHEN Type = 'CDMA'  THEN 'C'
		WHEN Type = 'LTE'   THEN 'L'
        WHEN Type = 'UMTS'  THEN 'D'
		WHEN Type = 'WCDM'  THEN 'D'
		WHEN Type = 'WCDMA' THEN 'D'
		WHEN Type = 'NR'    THEN 'N'
        WHEN Type = 'BT'    THEN 'B'
        WHEN Type = 'BLE'   THEN 'E'
	    WHEN Type = 'NFC'   THEN 'F'
        ELSE Type = NULL 
    END,
    RSSI, 
    CurrentLatitude, 
    CurrentLongitude
FROM observation WHERE FirstSeen != '1970-01-01 00:00:00'
ON CONFLICT(bssid) DO UPDATE SET
    ssid         = CASE WHEN excluded.lasttime > lasttime THEN excluded.ssid         ELSE ssid         END,
    frequency    = CASE WHEN excluded.lasttime > lasttime THEN excluded.frequency    ELSE frequency    END,
    capabilities = CASE WHEN excluded.lasttime > lasttime THEN excluded.capabilities ELSE capabilities END,
	lasttime     = CASE WHEN excluded.lasttime > lasttime THEN excluded.lasttime     ELSE lasttime     END,
    lastlat      = CASE WHEN excluded.lasttime > lasttime THEN excluded.lastlat      ELSE lastlat      END,
    lastlon      = CASE WHEN excluded.lasttime > lasttime THEN excluded.lastlon      ELSE lastlon      END,
    type         = CASE WHEN excluded.lasttime > lasttime THEN excluded.type         ELSE type         END,
 
    bestlevel    = CASE WHEN excluded.bestLevel > bestlevel THEN excluded.bestLevel   ELSE bestlevel   END,
    bestlat      = CASE WHEN excluded.bestLevel > bestlevel THEN excluded.bestlat     ELSE bestlat     END,
    bestlon      = CASE WHEN excluded.bestLevel > bestlevel THEN excluded.bestlon     ELSE bestlon     END;
");
        }


        public async Task AddObservationsFromDb(string path)
        {
            var fullPath = Path.GetFullPath(path);
            logger?.LogInformation("Import observations from {path}", fullPath);

            // ATTACH is scoped to a single connection, so the connection has to stay open for the
            // whole import. Open it explicitly instead of letting EF close it between statements.
            await context.Database.OpenConnectionAsync();
            try
            {
                var connection = context.Database.GetDbConnection();
                using (var command = connection.CreateCommand())
                {
                    // Bind the path rather than interpolating it: it comes from an upload and may
                    // contain quotes. A plain path is used rather than a file: URI with mode=ro,
                    // because SQLite only honours URI filenames when the main database was itself
                    // opened as a URI, which it is not.
                    command.CommandText = "ATTACH DATABASE $path AS importdb;";
                    var parameter = command.CreateParameter();
                    parameter.ParameterName = "$path";
                    parameter.Value = fullPath;
                    command.Parameters.Add(parameter);

                    await command.ExecuteNonQueryAsync();
                }

                try
                {
                    using var transaction = await context.Database.BeginTransactionAsync();

                    await ImportLocationsFromDbAsync();
                    await ImportNetworksFromDbAsync();

                    await transaction.CommitAsync();
                    await context.SaveChangesAsync();
                }
                finally
                {
                    // Connections are pooled, so a leftover attachment would break the next import
                    // with "database importdb is already in use".
                    using var detach = connection.CreateCommand();
                    detach.CommandText = "DETACH DATABASE importdb;";
                    await detach.ExecuteNonQueryAsync();
                }
            }
            finally
            {
                await context.Database.CloseConnectionAsync();
            }

            logger?.LogInformation("Import observations done");
        }

        private async Task ImportLocationsFromDbAsync()
        {
            await context.Database.ExecuteSqlAsync($@"INSERT OR IGNORE INTO location (bssid, level, lat, lon, altitude, accuracy, time, external)
SELECT bssid, level, lat, lon, altitude, accuracy, time, external
FROM importdb.location WHERE time > 0;");
        }

        private async Task ImportNetworksFromDbAsync()
        {
            await context.Database.ExecuteSqlAsync($@"INSERT INTO network (bssid, ssid, frequency, capabilities, lasttime, lastlat, lastlon, type, bestlevel, bestlat, bestlon)
SELECT bssid, ssid, frequency, capabilities, lasttime, lastlat, lastlon, type, bestlevel, bestlat, bestlon
FROM  importdb.network WHERE lasttime  > 0
ON CONFLICT(bssid) DO UPDATE SET
    ssid         = CASE WHEN excluded.lasttime > lasttime THEN excluded.ssid         ELSE ssid         END,
    frequency    = CASE WHEN excluded.lasttime > lasttime THEN excluded.frequency    ELSE frequency    END,
    capabilities = CASE WHEN excluded.lasttime > lasttime THEN excluded.capabilities ELSE capabilities END,
	lasttime     = CASE WHEN excluded.lasttime > lasttime THEN excluded.lasttime     ELSE lasttime     END,
    lastlat      = CASE WHEN excluded.lasttime > lasttime THEN excluded.lastlat      ELSE lastlat      END,
    lastlon      = CASE WHEN excluded.lasttime > lasttime THEN excluded.lastlon      ELSE lastlon      END,
    type         = CASE WHEN excluded.lasttime > lasttime THEN excluded.type         ELSE type         END,
 
    bestlevel    = CASE WHEN excluded.bestLevel > bestlevel THEN excluded.bestLevel   ELSE bestlevel   END,
    bestlat      = CASE WHEN excluded.bestLevel > bestlevel THEN excluded.bestlat     ELSE bestlat     END,
    bestlon      = CASE WHEN excluded.bestLevel > bestlevel THEN excluded.bestlon     ELSE bestlon     END;
");
        }

        public async IAsyncEnumerable<Network> GetNetworksAsync(
            NetworkSearchFilter filter,
            [EnumeratorCancellation] CancellationToken ct
        )
        {
            logger?.LogInformation("Getting networks");

            var query = filter.GetFilterQuery();
            var dbQuery = context.Networks.Where(SqliteExpressionOptimizer.Transform(query));

            await foreach (var network in dbQuery.AsAsyncEnumerable().WithCancellation(ct))
                yield return network;
        }
    }
}
