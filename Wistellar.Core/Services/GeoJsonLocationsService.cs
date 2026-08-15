using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NetTopologySuite.Features;
using NetTopologySuite.Geometries;
using NetTopologySuite.IO.Converters;
using System.Linq.Expressions;
using Wistellar.ExpressionVisitors;

namespace Wistellar.Core.Services
{
    public class LocationSearchFilter
    {
        public string[] Bssid = [];
        public MinMaxRange<int?> Altitude = new(null, null);
    }

    public class GeoJsonLocationsService(WiGleBackupContext context)
    {
        const int CoordinatesAccuracy = 6;

        private Expression<Func<Entities.Location, bool>> GetFilterQuery(LocationSearchFilter filter)
        {
            return (Entities.Location loc) =>
               (filter.Bssid.Length == 0 || filter.Bssid.Contains(loc.Bssid)) &&
                (filter.Altitude.Min == null || loc.Altitude > filter.Altitude.Min) &&
                (filter.Altitude.Max == null || loc.Altitude < filter.Altitude.Max);
        }

        public IEnumerable<Feature> Get(LocationSearchFilter filter)
        {
            var filterQuery = GetFilterQuery(filter);

            return context.Locations
                .Where(SqliteExpressionOptimizer.Transform(filterQuery))
                .Include(loc => loc.Network)
                .Select(loc => new Feature()
                {
                    Geometry = new Point(new Coordinate(
                        Math.Round(loc.Lon, CoordinatesAccuracy),
                        Math.Round(loc.Lat, CoordinatesAccuracy))
                    ),
                    Attributes = new AttributesTable(new Dictionary<string, object?>
                    {
                        { GeoJsonConverterFactory.DefaultIdPropertyName, loc.Id.ToString() },
                        { "type", loc.Network == null ? null : loc.Network.Type },
                        { "bssid", loc.Bssid },
                        { "altitude", Math.Round(loc.Altitude, 2) },
                        { "level", loc.Level },
                        { "accuracy", Math.Round(loc.Accuracy, 2) },
                        { "time", loc.Time },
                        { "ssid", loc.Network == null ? null : loc.Network.SSID },
                        { "capabilities", loc.Network == null ? null : loc.Network.Capabilities },
                    })
                });
        }
    }
}
