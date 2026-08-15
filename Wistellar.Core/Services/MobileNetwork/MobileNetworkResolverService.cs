using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Wistellar.Core.Entities;

namespace Wistellar.Core.Services.MobileNetwork
{
    public class MobileNetworkResolverService(
        MccMncFetchService fetchService,
        MccMncDbService dbService,
            ILoggerFactory loggerFactory
        ) : IMobileNetworkResolverService, IHostedLifecycleService
    {
        private Dictionary<string, string> mobileNetworks = [];
        private readonly ILogger? logger = loggerFactory?.CreateLogger<MobileNetworkResolverService>();


        public string? Get(string plmn)
        {

            return mobileNetworks.TryGetValue(plmn, out string? value) ? value : null;
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
            logger?.LogInformation("Loading MCC MNC");
            var networks = new Dictionary<string, string>();
            await foreach (var record in dbService.Get())
            {
                var key = record.PLMN.ToString();
                networks[key] = string.Intern(record.Operator ?? "[no name]");
            }

            if (networks.Count == 0)
            {
                List<MccMnc> records = [];

                await foreach (var record in fetchService.Get())
                {
                    records.Add(record);
                    var key = record.PLMN.ToString();
                    networks[key] = string.Intern(record.Operator ?? "[no name]");
                }

                await dbService.Add(records.GroupBy(x => x.PLMN).Select(x => x.Last()));
            }
            mobileNetworks = networks;
            logger?.LogInformation("Loading MCC MNC DONE");
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
