using System.Xml.Linq;
using Wistellar.Core.Import;
using Wistellar.Core.Models;
using Wistellar.Tests.Support;

namespace Wistellar.Tests.Import;

[TestFixture]
[Category("Unit")]
public sealed class WifiDbImportTests
{
    private RecordingLogger<WifiDbImport> logger = null!;
    private WifiDbImport importer = null!;

    [SetUp]
    public void SetUp()
    {
        logger = new RecordingLogger<WifiDbImport>();
        importer = new WifiDbImport(logger);
    }

    [Test]
    public async Task ReadsObservationFields()
    {
        var observation = (await ParseDescription()).Single();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(observation.MAC, Is.EqualTo("02:00:00:00:00:01"));
            Assert.That(observation.SSID, Is.EqualTo("Test network"));
            Assert.That(observation.AuthMode, Is.EqualTo("Open"));
            Assert.That(observation.FirstSeen, Is.EqualTo(new DateTime(2026, 10, 25, 12, 0, 0)));
            Assert.That(observation.Channel, Is.EqualTo(1));
            Assert.That(observation.RSSI, Is.EqualTo(-55));
            Assert.That(observation.CurrentLatitude, Is.EqualTo(52.52));
            Assert.That(observation.CurrentLongitude, Is.EqualTo(13.405));
            Assert.That(observation.AltitudeMeters, Is.EqualTo(34));
            Assert.That(observation.MfgrId, Is.EqualTo("Test manufacturer"));
            Assert.That(observation.Type, Is.EqualTo("WIFI"));
            Assert.That(logger.Warnings, Is.Empty);
        }
    }

    [TestCase("WPA2_Enterprise", "CCMP")]
    [TestCase("wpa2_enterprise", "ccmp")]
    [TestCase("WPA2-Enterprise", "CCMP")]
    [TestCase("WPA-Enterprise", "TKIP")]
    [TestCase("WPA2-Personal", "CCMP")]
    [TestCase("WPA3-Personal", "GCMP")]
    [TestCase("Shared Key", "WEP")]
    [TestCase("Firmenweiter WPA", "AES")]
    public async Task RecognizesSecurityLabelsAndPreservesOriginalAuthentication(string authentication, string encryption)
    {
        var description = KmlFixture.WithField("Authentication", authentication)
            .Replace("<b>Encryption: </b>None", $"<b>Encryption: </b>{encryption}");

        var observation = (await ParseDescription(description)).Single();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(observation.AuthMode, Is.EqualTo(authentication));
            Assert.That(logger.Warnings, Is.Empty);
        }
    }

    [TestCase("Authentication", "Future_Authentication")]
    [TestCase("Encryption", "Future_Cipher")]
    public async Task WarnsForUnknownSecurityLabelsWithoutDroppingTheObservation(string field, string value)
    {
        var observations = await ParseDescription(KmlFixture.WithField(field, value));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(observations, Has.Length.EqualTo(1));
            Assert.That(logger.Warnings, Has.Count.EqualTo(1));
            Assert.That(logger.Warnings.Single(), Does.Contain(field).And.Contain(value).And.Contain("fixture.kml"));
            if (field == "Authentication") Assert.That(observations[0].AuthMode, Is.EqualTo(value));
        }
    }

    [Test]
    public async Task EmptyRssiDoesNotReadTheFollowingManufacturerField()
    {
        // The compact export has no whitespace text node between the empty RSSI and the next label.
        var description = string.Concat(KmlFixture.Description.Split('\n').Select(line => line.Trim()))
            .Replace("<b>High GPS RSSI: </b>-55", "<b>High GPS RSSI: </b>")
            .Replace("<b>Manufacturer: </b>Test manufacturer", "<b>Manufacturer: </b>Unknown Manufacturer");

        var observation = (await ParseDescription(description)).Single();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(observation.RSSI, Is.EqualTo(-200));
            Assert.That(observation.MfgrId, Is.EqualTo("Unknown Manufacturer"));
            Assert.That(observation.Channel, Is.EqualTo(1));
            Assert.That(logger.Warnings, Has.Count.EqualTo(1));
            Assert.That(logger.Warnings.Single(), Does.Contain("unknown RSSI").And.Contain("fixture.kml"));
        }
    }

    [TestCase("")]
    [TestCase("   ")]
    [TestCase("Unknown")]
    [TestCase("2147483648")]
    public async Task UnavailableRssiKeepsTheObservationWithUnknownSignal(string value)
    {
        var observation = (await ParseDescription(KmlFixture.WithField("High GPS RSSI", value))).Single();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(observation.RSSI, Is.EqualTo(-200));
            Assert.That(observation.MfgrId, Is.EqualTo("Test manufacturer"));
            Assert.That(logger.Warnings, Has.Count.EqualTo(1));
        }
    }

    [Test]
    public async Task MissingRssiKeepsTheObservationWithUnknownSignal()
    {
        var description = KmlFixture.Description.Replace("<b>High GPS RSSI: </b>-55<br/>", "");

        var observation = (await ParseDescription(description)).Single();

        Assert.That(observation.RSSI, Is.EqualTo(-200));
        Assert.That(logger.Warnings, Has.Count.EqualTo(1));
    }

    [Test]
    public async Task ReportsUnknownSignalOncePerFileAndPreservesValidSignals()
    {
        var document = KmlFixture.Document(
            KmlFixture.Placemark(KmlFixture.WithField("High GPS RSSI", "")),
            KmlFixture.Placemark(),
            KmlFixture.Placemark(KmlFixture.WithField("High GPS RSSI", "Unknown")));

        var observations = await ParseDocument(document);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(observations.Select(observation => observation.RSSI), Is.EqualTo(new[] { -200, -55, -200 }));
            Assert.That(logger.Warnings, Has.Count.EqualTo(1));
            Assert.That(logger.Warnings.Single(), Does.Contain("2 observations").And.Contain("-200"));
        }
    }

    [TestCase(" -55 ", -55)]
    [TestCase("<span>-55</span>", -55)]
    [TestCase("<b>-55</b>", -55)]
    [TestCase("<span>&#45;55</span>", -55)]
    public async Task ReadsNumericValuesInsideInlineMarkup(string value, int expectedRssi)
    {
        var description = KmlFixture.WithField("High GPS RSSI", value)
            .Replace("<b>High GPS RSSI: </b>", "<b>High GPS RSSI:</b>");

        var observation = (await ParseDescription(description)).Single();

        Assert.That(observation.RSSI, Is.EqualTo(expectedRssi));
        Assert.That(logger.Warnings, Is.Empty);
    }

    [TestCase("<b>Manufacturer: </b><br/><b>SecType: </b>3", "")]
    [TestCase("<b>Manufacturer: </b><b>SecType: </b>3", "")]
    [TestCase("<b>Manufacturer:</b> <span>Acme &amp; Sons</span><br/>", "Acme & Sons")]
    [TestCase("<b>Manufacturer: </b><!-- comment -->Test manufacturer<br/>", "Test manufacturer")]
    public async Task ReadsOnlyTheManufacturerField(string replacement, string expected)
    {
        var description = KmlFixture.Description.Replace("<b>Manufacturer: </b>Test manufacturer<br/>", replacement);

        var observation = (await ParseDescription(description)).Single();

        Assert.That(observation.MfgrId, Is.EqualTo(expected));
        Assert.That(observation.RSSI, Is.EqualTo(-55));
    }

    [TestCase("")]
    [TestCase("Unknown")]
    public async Task MissingOrInvalidChannelIsNullable(string value)
    {
        var description = KmlFixture.Description.Replace("<b>Channel: </b>1", $"<b>Channel: </b>{value}");

        var observation = (await ParseDescription(description)).Single();

        Assert.That(observation.Channel, Is.Null);
        Assert.That(observation.AuthMode, Is.EqualTo("Open"));
        Assert.That(observation.RSSI, Is.EqualTo(-55));
    }

    [Test]
    public async Task ConvertsTheBlankSsidPlaceholderToAnEmptyName()
    {
        var observation = (await ParseDescription(KmlFixture.Description.Replace("Test network", "[Blank SSID]"))).Single();

        Assert.That(observation.SSID, Is.Empty);
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("0,0,0")]
    [TestCase("13.405,52.52")]
    public async Task SkipsPlacemarksWithoutAUsablePosition(string? coordinates)
    {
        var document = KmlFixture.Document(KmlFixture.Placemark(coordinates: coordinates), KmlFixture.Placemark());

        var observations = await ParseDocument(document);

        Assert.That(observations, Has.Length.EqualTo(1));
        Assert.That(observations[0].RSSI, Is.EqualTo(-55));
    }

    [Test]
    public async Task SkipsPlacemarksWithoutDescriptions()
    {
        var document = KmlFixture.Document(KmlFixture.Placemark(description: null), KmlFixture.Placemark());

        Assert.That(await ParseDocument(document), Has.Length.EqualTo(1));
    }

    [Test]
    public async Task NetworkLinksWithoutObservationsProduceAnEmptyResult()
    {
        XNamespace ns = "http://www.opengis.net/kml/2.2";
        var document = KmlFixture.Document(new XElement(ns + "NetworkLink",
            new XElement(ns + "Link", new XElement(ns + "href", "https://example.invalid/survey.kmz"))));

        Assert.That(await ParseDocument(document), Is.Empty);
        Assert.That(logger.Warnings, Is.Empty);
    }

    [Test]
    [SetCulture("de-DE")]
    public async Task CoordinatesUseInvariantCulture()
    {
        var observation = (await ParseDescription()).Single();

        Assert.That(observation.CurrentLatitude, Is.EqualTo(52.52));
        Assert.That(observation.CurrentLongitude, Is.EqualTo(13.405));
        Assert.That(observation.AltitudeMeters, Is.EqualTo(34));
    }

    private Task<Observation[]> ParseDescription(string description = KmlFixture.Description) =>
        ParseDocument(KmlFixture.DocumentWithDescription(description));

    private async Task<Observation[]> ParseDocument(string document)
    {
        using var reader = new StringReader(document);
        return (await importer.Import("fixture.kml", reader)).ToArray();
    }
}
