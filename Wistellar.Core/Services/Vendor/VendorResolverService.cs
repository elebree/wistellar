using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Wistellar.Core.Models;

namespace Wistellar.Core.Services.Vendor
{
    public class VendorResolverService(
        OuiFetchService sourceServiceFile,
        OuiDbService sourceServiceDb,
            ILoggerFactory loggerFactory
        ) : IVendorResolverService, IHostedLifecycleService
    {
        private Dictionary<string, string> ouiRecords = [];
        //    private readonly IOuiSourceService? sourceService;
        //    private readonly OuiSourceServiceDb? sourceServiceDb;
        private readonly ILogger? logger = loggerFactory?.CreateLogger<VendorResolverService>();


        public string? Get(string mac)
        {
            var key = mac.Replace(":", "").Substring(0, 6).ToUpper();
            return ouiRecords.TryGetValue(key, out string? value) ? value : null;
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            //  throw new NotImplementedException();
            return Task.CompletedTask;
        }

        public Task StartedAsync(CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        public async Task StartingAsync(CancellationToken cancellationToken)
        {
            logger?.LogInformation("Loading OUI");
            var orgs = new Dictionary<string, string>();
            await foreach (var record in sourceServiceDb.Get())
            {
                var key = record.Base16.ToUpper();
                orgs[key] = string.Intern(record.Organization);
            }

            if (orgs.Count == 0)
            {
                List<OuiVendorInfo> records = [];
                await foreach (var record in sourceServiceFile.Get())
                {
                    var key = record.Base16.ToUpper();
                    records.Add(record);
                    orgs[key] = string.Intern(record.Organization);
                }
                await sourceServiceDb.AddOui(records);
            }
            ouiRecords = orgs;
            logger?.LogInformation("Loading OUI DONE");
        }

        public Task StoppedAsync(CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        public Task StoppingAsync(CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }
    }
}
