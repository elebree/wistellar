using Microsoft.AspNetCore.Mvc;

namespace Wistellar.Server.Controllers
{
    public record QueryCoordinatesRange
    {
        [FromQuery(Name = "lat[gt]")] public double? latGt { get; set; }

        [FromQuery(Name = "lat[lt]")] public double? latLt { get; set; }

        [FromQuery(Name = "lon[gt]")] public double? lonGt { get; set; }

        [FromQuery(Name = "lon[lt]")] public double? lonLt { get; set; }
    }
}
