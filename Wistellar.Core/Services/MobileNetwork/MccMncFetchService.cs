using CsvHelper;
using CsvHelper.Configuration;
using Microsoft.Extensions.Logging;
using System.Globalization;
using System.Text;
using Wistellar.Core.Entities;

namespace Wistellar.Core.Services.MobileNetwork
{
    public partial class MccMncFetchService(
        string path,
        ILoggerFactory? loggerFactory
    ) : IMccMncService
    {
        private readonly ILogger? logger = loggerFactory?.CreateLogger<MccMncFetchService>();

        //public OuiSourceService(ILoggerFactory loggerFactory) : this(loggerFactory, "oui.txt")
        public MccMncFetchService(ILoggerFactory? loggerFactory) : this(
            "https://mcc-mnc.net/mcc-mnc.csv",
            loggerFactory)
        {
        }

        public async IAsyncEnumerable<MccMnc> Get()
        {
            Stream? stream = null;

            try
            {
                logger?.LogInformation("Loading MCC MNC data from {source}", path);
                // Determine if the input is a URL or a file path.
                if (Uri.TryCreate(path, UriKind.Absolute, out Uri? uri) && uri.Scheme.StartsWith("http"))
                {
                    // If the input is a URL, download the file using HttpClient.
                    using HttpClient client = new();
                    stream = await client.GetStreamAsync(path);
                }
                else
                {
                    // Otherwise, treat the input as a local file path and open a FileStream.
                    stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
                }


                using var reader = new StreamReader(stream);
                var csv = new CsvReader(reader, new CsvConfiguration(CultureInfo.InvariantCulture)
                { Delimiter = ";", Encoding = Encoding.UTF8 });

                var items = csv.GetRecordsAsync<MccMnc>();
                // Parse the file content from the stream.
                await foreach (var item in items)
                {
                    yield return item;
                }
                logger?.LogInformation("Loading MCC MNC data from {source} done", path);
            }
            finally
            {
                // Ensure the stream is closed when done.
                stream?.Dispose();
            }
        }
    }

}
