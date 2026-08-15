using EFCore.BulkExtensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Wistellar.Core.Entities;

namespace Wistellar.Core.Services.MobileNetwork
{
    public partial class MccMncDbService(
        ILoggerFactory loggerFactory,
        WiGleBackupContext context) : IMccMncService
    {
        ILogger? logger = loggerFactory?.CreateLogger<MccMncDbService>();

        public async IAsyncEnumerable<MccMnc> Get()
        {
            await foreach (var record in context.MccMnc)
            {
                yield return record;
            }
        }

        public async Task Add(IEnumerable<MccMnc> records)
        {
            logger?.LogInformation("Import MccMnc");

            using var transaction = await context.Database.BeginTransactionAsync();

            var config = new BulkConfig()
            {
                CustomDestinationTableName = "mccmnc",
                UseTempDB = false,
            };

            await context.BulkInsertOrUpdateAsync(records.ToArray(), config);

            await transaction.CommitAsync();
            await context.SaveChangesAsync();

            logger?.LogInformation("Import MccMnc done");
        }

    }
}
