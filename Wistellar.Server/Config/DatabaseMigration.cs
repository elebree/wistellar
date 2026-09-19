using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Wistellar.Core;

namespace Wistellar.Server.Config
{
    public static class DatabaseMigration
    {
        /// <summary>
        /// Applies pending migrations once, at startup.
        /// </summary>
        /// <remarks>
        /// This deliberately does not live in the <see cref="WiGleBackupContext"/> factory. EF Core
        /// acquires an exclusive migration lock (the <c>__EFMigrationsLock</c> table) on every
        /// <see cref="RelationalDatabaseFacadeExtensions.Migrate"/> call, whether or not anything is
        /// pending, and taking that lock is a write transaction. The database runs in rollback
        /// journal mode, where a writer locks the whole file against readers, so migrating per
        /// resolution serialized every request against every other one - measured at up to 1.9s of
        /// pure lock wait per request with eight concurrent tile requests, against ~30ms uncontended.
        ///
        /// Must run before the host starts: the OUI and MCC-MNC caches read their tables from
        /// <c>IHostedLifecycleService.StartingAsync</c>, so the schema has to exist by then.
        /// </remarks>
        public static void MigrateDatabase(this IServiceProvider services)
        {
            using var scope = services.CreateScope();
            var logger = scope.ServiceProvider
                .GetRequiredService<ILoggerFactory>()
                .CreateLogger(typeof(DatabaseMigration).FullName!);

            var stopwatch = Stopwatch.StartNew();
            var context = scope.ServiceProvider.GetRequiredService<WiGleBackupContext>();
            context.Database.Migrate();

            logger.LogInformation("Database migrated in {elapsed} ms", stopwatch.ElapsedMilliseconds);
        }
    }
}
