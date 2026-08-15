using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NetTopologySuite.Features;
using NetTopologySuite.Geometries;
using NetTopologySuite.IO.VectorTiles;
using NetTopologySuite.IO.VectorTiles.Mapbox;
using Wistellar.Core.Extensions;
using Wistellar.Core.Services;
namespace Wistellar.Server.Controllers
{
    [ApiController]
    [Route("geo/[action]")]
    [Authorize]
    public class GeoJsonController(
        ILogger<GeoJsonController> logger,
        GeoJsonNetworksService networkService,
        GeoJsonLocationsService geoJsonLocationsService
    ) : ControllerBase
    {
        private readonly GeoJsonNetworksService _geoJsonNetworksService = networkService;
        private readonly GeoJsonLocationsService _geoJsonLocationsService = geoJsonLocationsService;

        private static Envelope GetTileBoundingBox(int z, int x, int y)
        {
            double n = Math.Pow(2, z);
            double lon(int x) => x / n * 360.0 - 180.0;
            double lat(int y) => Math.Atan(Math.Sinh(Math.PI * (1 - 2 * y / n))) * 180.0 / Math.PI;
            return new Envelope(
                lon(x),
                lon(x + 1),
                lat(y + 1),
                lat(y));
        }

        [HttpGet("{z}/{x}/{y}.pbf")]
        [ActionName("tiles")]
        public async Task GetNetworks(
        [FromQuery] QueryNetworkFilter query,
           int z,
           int x,
           int y
        )
        {
            logger.LogDebug("Building tile {z}/{x}/{y}", z, x, y);

            var bounds = GetTileBoundingBox(z, x, y);
            var features = GetNetworkFeatures(
                query,
                new QueryCoordinatesRange()
                {
                    latGt = bounds.MinY,
                    latLt = bounds.MaxY,
                    lonGt = bounds.MinX,
                    lonLt = bounds.MaxX
                },
                false
            );

            // Define which tile to create.
            var tileDefinition = new NetTopologySuite.IO.VectorTiles.Tiles.Tile(x, y, z);
            // Create a vector tile instance and pass om the tile ID from the tile definition above.
            var tile = new VectorTile { TileId = tileDefinition.Id };

            // Create layers.
            var wifiLayer = new Layer() { Name = "wifi" };
            var bluetoothLayer = new Layer() { Name = "bluetooth" };
            var cellLayer = new Layer() { Name = "cell" };

            // Split the features across layers by network type, so the map style can show and
            // hide wifi, bluetooth and cell independently.
            await foreach (var feature in features.WithCancellation(HttpContext.RequestAborted))
            {
                switch (feature.Attributes["type"])
                {
                    case "W":
                        wifiLayer.Features.Add(feature);
                        break;
                    case "B":
                    case "E":
                    case "F":
                        bluetoothLayer.Features.Add(feature);
                        break;
                    case "G":
                    case "C":
                    case "L":
                    case "D":
                    case "N":
                        cellLayer.Features.Add(feature);
                        break;
                }
            }

            tile.Layers.Add(wifiLayer);
            tile.Layers.Add(bluetoothLayer);
            tile.Layers.Add(cellLayer);

            // Set response headers
            Response.ContentType = "application/vnd.mapbox-vector-tile";
            Response.Headers.CacheControl = "max-age=300, public";

            // Written straight to the response body rather than buffered into memory first.
            tile.Write(
                Response.Body,
                MapboxTileWriter.DefaultMinLinealExtent,
                MapboxTileWriter.DefaultMinPolygonalExtent,
                4096
            );

            await Response.Body.FlushAsync(HttpContext.RequestAborted);
            HttpContext.RequestAborted.ThrowIfCancellationRequested();
        }

        private IAsyncEnumerable<Feature> GetNetworkFeatures(
            QueryNetworkFilter query,
            QueryCoordinatesRange range,
            bool withAttributes = true
    )
        {
            var filter = QueryToFilter(query);
            filter.BestLat = new MinMaxRange<double?>(range.latGt, range.latLt);
            filter.BestLon = new MinMaxRange<double?>(range.lonGt, range.lonLt);

            return _geoJsonNetworksService.Get(filter, HttpContext.RequestAborted, withAttributes);
        }

        [HttpGet]
        [ActionName("network")]
        public async Task<FeatureCollection> GetNetworks(
            [FromQuery] QueryNetworkFilter query,
            [FromQuery] QueryCoordinatesRange range,
            bool withAttributes = true
        )
        {
            var features = GetNetworkFeatures(query, range, withAttributes);
            return new FeatureCollection(features.ToBlockingEnumerable());
        }

        [HttpGet]
        [ActionName("location")]
        public FeatureCollection GetLocations(
            [FromQuery] string? bssid,
            [FromQuery(Name = "altitude[gt]")] int? altitudeGt,
            [FromQuery(Name = "altitude[lt]")] int? altitudeLt)
        {
            var filter = new LocationSearchFilter()
            {
                Bssid = bssid?.Split("|") ?? [],
                Altitude = new MinMaxRange<int?>(altitudeGt, altitudeLt),
            };
            return new FeatureCollection(_geoJsonLocationsService.Get(filter).ToList());
        }

        private static NetworkSearchFilter QueryToFilter(QueryNetworkFilter query)
        {
            static long? parseTime(string? datetime) => DateExtensions.TryParseDate(datetime, out var time) ? time.ToUnixTimeSeconds() : null;

            static long? parseDuration(string? text) =>
             DateExtensions.TryParseDuration(text, out var time) ? (long)time.TotalSeconds : null;

            return new NetworkSearchFilter()
            {
                SSID = query.SSID?.Split("|") ?? [],
                BSSID = query.BSSID?.Split("|") ?? [],
                Types = query.Type?.Split("|") ?? [],
                Capabilities = query.Capabilities,
                Range = new MinMaxRange<int?>(query.RangeGt, query.RangeLt),
                Locations = new MinMaxRange<int?>(query.LocationsGt, query.LocationsLt),
                LastSeen = new MinMaxRange<long?>(parseTime(query.LastSeenGt), parseTime(query.LastSeenLt)),
                Dwell = new MinMaxRange<long?>(parseDuration(query.DwellGt), parseDuration(query.DwellLt)),
            };
        }
    }
}
