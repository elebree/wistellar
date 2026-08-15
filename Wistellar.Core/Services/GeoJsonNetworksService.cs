using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NetTopologySuite.Features;
using NetTopologySuite.Geometries;
using NetTopologySuite.IO.Converters;
using System.Linq.Expressions;
using System.Runtime.CompilerServices;
using Wistellar.Core.Entities;
using Wistellar.Core.Services.Vendor;
using Wistellar.ExpressionVisitors;

namespace Wistellar.Core.Services
{
    public class MinMaxRange<T>(T min, T max)
    {
        public T Min { get; } = min;
        public T Max { get; } = max;
    }

    public class NetworkSearchFilter
    {
        public string[] SSID = [];
        public string[] BSSID = [];
        public string[] Types = [];
        public string? Capabilities;
        public MinMaxRange<int?> Range = new(null, null);
        public MinMaxRange<long?> Dwell = new(null, null);
        public MinMaxRange<int?> Locations = new(null, null);
        public MinMaxRange<long?> LastSeen = new(null, null);
        public MinMaxRange<double?> BestLat = new(null, null);
        public MinMaxRange<double?> BestLon = new(null, null);

        /// <summary>
        /// Compiles the whole filter into a single predicate. Every clause is written as
        /// <c>X == null || ...</c> so that unset fields collapse away, and the bounds are embedded
        /// with <see cref="EF.Constant{T}(T)"/> rather than passed as parameters — SQLite's planner
        /// only uses the composite index on <c>network</c> when it can see the literal values.
        /// </summary>
        public Expression<Func<Network, bool>> GetFilterQuery()
        {
            var filter = this;
            bool searchBssidExact = filter.BSSID.Length > 0 && !filter.BSSID.Any(b => b.Contains('%'));
            bool searchBssidLike = filter.BSSID.Length > 0 && filter.BSSID.Any(b => b.Contains('%'));

            return (v) =>
            !(filter.SSID.Length > 0 && !filter.SSID.Any(s => EF.Functions.Like(v.SSID, s))) &&
            (!searchBssidLike || filter.BSSID.Any(s => EF.Functions.Like(v.BSSID, s))) &&
            (!searchBssidExact || filter.BSSID.Contains(v.BSSID)) &&
            (filter.Capabilities == null || EF.Functions.Like(v.Capabilities, filter.Capabilities)) &&
            (filter.Range.Min == null || v.Range > filter.Range.Min) &&
            (filter.Range.Max == null || v.Range < filter.Range.Max) &&
            (filter.Locations.Min == null || v.Observations > EF.Constant(filter.Locations.Min)) &&
            (filter.Locations.Max == null || v.Observations < EF.Constant(filter.Locations.Max)) &&
            (filter.LastSeen.Min == null || v.LastSeen > EF.Constant(filter.LastSeen.Min * 1000)) &&
            (filter.LastSeen.Max == null || v.LastSeen < EF.Constant(filter.LastSeen.Max * 1000)) &&
            (filter.Dwell.Min == null || v.Dwell > EF.Constant(filter.Dwell.Min)) &&
            (filter.Dwell.Max == null || v.Dwell < EF.Constant(filter.Dwell.Max)) &&

            (filter.BestLat.Max == null || v.BestLatitude < EF.Constant(filter.BestLat.Max)) &&
            (filter.BestLat.Min == null || v.BestLatitude > EF.Constant(filter.BestLat.Min)) &&
            (filter.BestLon.Max == null || v.BestLongitude < EF.Constant(filter.BestLon.Max)) &&
            (filter.BestLon.Min == null || v.BestLongitude > EF.Constant(filter.BestLon.Min)) &&
            (!filter.Types.Any() || EF.Constant(filter.Types).Contains(v.Type)
            );
        }

    }

    public class GeoJsonNetworksService(
        ILoggerFactory loggerFactory,
        WiGleBackupContext context,
        IVendorResolverService vendorResolverService
    )
    {
        const int CoordinatesAccuracy = 6;
        ILogger? logger = loggerFactory?.CreateLogger<GeoJsonNetworksService>();

        public async IAsyncEnumerable<Feature> Get(NetworkSearchFilter filter, [EnumeratorCancellation] CancellationToken ct, bool withAttributes = false)
        {

            logger?.LogInformation("Getting networks");

            var query = filter.GetFilterQuery();

            IQueryable<Feature> dbQuery = context.Networks.Where(SqliteExpressionOptimizer.Transform(query))
              .Select(
                (network) =>
                new Feature()
                {
                    Geometry = new Point(
                        Math.Round(network.BestLongitude, CoordinatesAccuracy),
                        Math.Round(network.BestLatitude, CoordinatesAccuracy)
                        ),

                    // Tiles only need enough to identify a feature and pick its layer; the SPA
                    // fetches the full attribute set separately when a feature is clicked.
                    Attributes = withAttributes ? new AttributesTable(new Dictionary<string, object?>
                    {
                        { GeoJsonConverterFactory.DefaultIdPropertyName,  network.BSSID },
                        { "type",network.Type },
                        { "bssid",network.BSSID },
                        { "ssid",network.SSID },
                        { "cap",network.Capabilities },
                        { "lasttime",network.LastSeen },
                        { "lastlat", Math.Round(network.Lastlatitude, CoordinatesAccuracy) },
                        { "lastlon", Math.Round(network.Lastlongitude, CoordinatesAccuracy) },
                        { "level",network.BestLevel },
                        { "loc", network.Observations },
                        { "dist", network.Range },
                        { "dur", network.Dwell },
                        { "vendor", vendorResolverService.Get(network.BSSID) },
                    })
                    : new AttributesTable(new Dictionary<string, object?>
                    {
                        { GeoJsonConverterFactory.DefaultIdPropertyName,  network.BSSID },
                        { "type",network.Type },
                    })
                });

            if (logger?.IsEnabled(LogLevel.Debug) == true)
                logger.LogDebug("Networks query: {query}", dbQuery.ToQueryString());

            await foreach (var feature in dbQuery.AsAsyncEnumerable().WithCancellation(ct))
                yield return feature;

            logger?.LogInformation("Getting networks done");
        }
    }
}
