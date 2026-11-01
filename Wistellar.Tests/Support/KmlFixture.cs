using System.Xml.Linq;

namespace Wistellar.Tests.Support;

internal static class KmlFixture
{
    private static readonly XNamespace Kml = "http://www.opengis.net/kml/2.2";

    public const string Description = """
        <a>Test network</a><br/>
        <b>Mac: </b>02:00:00:00:00:01<br/>
        <b>Authentication: </b>Open<br/>
        <b>Encryption: </b>None<br/>
        <b>Last Active: </b>2026-10-25 12:00:00<br/>
        <b>Channel: </b>1<br/>
        <b>High GPS RSSI: </b>-55<br/>
        <b>Manufacturer: </b>Test manufacturer<br/>
        """;

    public static XElement Placemark(string? description = Description, string? coordinates = "13.405,52.52,34") =>
        new(Kml + "Placemark",
            description is null ? null : new XElement(Kml + "description", new XCData(description)),
            coordinates is null ? null : new XElement(Kml + "Point", new XElement(Kml + "coordinates", coordinates)));

    public static string Document(params XElement[] placemarks) =>
        new XDocument(new XElement(Kml + "kml", new XElement(Kml + "Document", placemarks)))
            .ToString(SaveOptions.DisableFormatting);

    public static string DocumentWithDescription(string description = Description) => Document(Placemark(description));

    public static string WithField(string label, string value) => label switch
    {
        "Authentication" => Description.Replace("<b>Authentication: </b>Open", $"<b>Authentication: </b>{value}"),
        "Encryption" => Description.Replace("<b>Encryption: </b>None", $"<b>Encryption: </b>{value}"),
        "High GPS RSSI" => Description.Replace("<b>High GPS RSSI: </b>-55", $"<b>High GPS RSSI: </b>{value}"),
        _ => throw new ArgumentException($"Unknown fixture field: {label}", nameof(label)),
    };
}
