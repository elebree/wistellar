using EFCore.BulkExtensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Wistellar.Core.Entities;
using Wistellar.Core.Models;

namespace Wistellar.Core.Services.Vendor
{
    public partial class OuiDbService(
        ILoggerFactory loggerFactory,
        WiGleBackupContext context) : IOuiSourceService
    {
        ILogger? logger = loggerFactory?.CreateLogger<OuiDbService>();

        public async IAsyncEnumerable<OuiVendorInfo> Get()
        {
            await foreach (var r in context.Oui)
            {
                yield return new OuiVendorInfo()
                {
                    Base16 = r.Base16,
                    Address = r.Address,
                    City = r.City,
                    Country = r.Country,
                    MacAddress = r.MacAddress,
                    Organization = r.Organization
                };
            }
        }

        public async Task AddOui(IEnumerable<OuiVendorInfo> oui)
        {
            logger?.LogInformation("Import OUI");

            using var transaction = await context.Database.BeginTransactionAsync();

            var config = new BulkConfig()
            {
                CustomDestinationTableName = "oui",
                UseTempDB = false,
            };

            var records = oui.Select(o => new OUI()
            {
                Base16 = o.Base16,
                Address = o.Address,
                City = o.City,
                Country = o.Country,
                MacAddress = o.MacAddress,
                Organization = o.Organization
            }).ToArray();

            await context.BulkInsertOrUpdateAsync(records, config);

            await transaction.CommitAsync();
            await context.SaveChangesAsync();

            logger?.LogInformation("Import OUI done");
        }

    }
}
