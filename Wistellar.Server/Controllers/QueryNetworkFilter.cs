using Microsoft.AspNetCore.Mvc;

namespace Wistellar.Server.Controllers
{
    public record QueryNetworkFilter
    {
        [FromQuery(Name = "ssid")] public string? SSID { get; set; }
        [FromQuery(Name = "bssid")] public string? BSSID { get; set; }
        [FromQuery(Name = "type")] public string? Type { get; set; }
        [FromQuery(Name = "cap")] public string? Capabilities { get; set; }
        [FromQuery(Name = "vendor")] public string? Vendor { get; set; }
        [FromQuery(Name = "range[gt]")] public int? RangeGt { get; set; }
        [FromQuery(Name = "range[lt]")] public int? RangeLt { get; set; }
        [FromQuery(Name = "locations[gt]")] public int? LocationsGt { get; set; }
        [FromQuery(Name = "locations[lt]")] public int? LocationsLt { get; set; }
        [FromQuery(Name = "time[gt]")] public string? LastSeenGt { get; set; }
        [FromQuery(Name = "time[lt]")] public string? LastSeenLt { get; set; }
        [FromQuery(Name = "dwell[gt]")] public string? DwellGt { get; set; }
        [FromQuery(Name = "dwell[lt]")] public string? DwellLt { get; set; }
    }
}
