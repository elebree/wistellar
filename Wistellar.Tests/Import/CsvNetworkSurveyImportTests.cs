using Microsoft.Extensions.Logging.Abstractions;
using Wistellar.Core;
using Wistellar.Core.Import;
using Wistellar.Core.Models;
using Wistellar.Core.Services.MobileNetwork;
using Wistellar.Server.Import;
using Wistellar.Tests.Support;
using static Wistellar.Tests.Support.NetworkSurveyFixture;

namespace Wistellar.Tests.Import;

[TestFixture]
[Category("Unit")]
public sealed class CsvNetworkSurveyImportTests
{
    private const string Prefix = "2026-03-29T19:58:23.304968+02:00," + DotLocation + ",NS 0000000000000000 20260329-195802";

    private WiGleBackupContext lookupContext = null!;
    private MobileNetworkResolverService resolver = null!;
    private RecordingLogger<CsvNetworkSurveyImport> logger = null!;
    private CsvNetworkSurveyImport importer = null!;

    [SetUp]
    public void SetUp()
    {
        // Lookup uses an empty in-memory dictionary; hosted startup and downloads are never run.
        lookupContext = new WiGleBackupContext();
        resolver = new MobileNetworkResolverService(new MccMncFetchService(NullLoggerFactory.Instance),
            new MccMncDbService(NullLoggerFactory.Instance, lookupContext), NullLoggerFactory.Instance);
        logger = new RecordingLogger<CsvNetworkSurveyImport>();
        importer = new CsvNetworkSurveyImport(logger, resolver);
    }

    [TearDown]
    public void TearDown() => lookupContext.Dispose();

    [Test]
    public async Task ReadsWifiRecordsWrittenWithADecimalComma()
    {
        var observation = (await Import(Document(WifiColumns, WifiRow()))).Single();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(observation.Type, Is.EqualTo("WIFI"));
            Assert.That(observation.MAC, Is.EqualTo("02:00:00:00:00:01"));
            Assert.That(observation.SSID, Is.EqualTo("Test network"));
            Assert.That(observation.AuthMode, Is.EqualTo("[WPA3]"));
            Assert.That(observation.FirstSeen, Is.EqualTo(DateTimeOffset.Parse("2026-03-29T17:58:26.287411Z").UtcDateTime));
            Assert.That(observation.CurrentLatitude, Is.EqualTo(52.52));
            Assert.That(observation.CurrentLongitude, Is.EqualTo(13.405));
            Assert.That(observation.AltitudeMeters, Is.EqualTo(34.5));
            Assert.That(observation.AccuracyMeters, Is.EqualTo(11));
            Assert.That(observation.RSSI, Is.EqualTo(-70));
            Assert.That(observation.Channel, Is.EqualTo(1));
            Assert.That(observation.Frequency, Is.EqualTo(2412));
        }
    }

    [Test]
    public async Task ReadsRecordsWrittenWithADecimalPoint()
    {
        var observation = (await Import(Document(WifiColumns, WifiRow(location: DotLocation)))).Single();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(observation.CurrentLatitude, Is.EqualTo(52.52));
            Assert.That(observation.CurrentLongitude, Is.EqualTo(13.405));
            Assert.That(observation.AltitudeMeters, Is.EqualTo(34.5));
        }
    }

    [TestCase("WPA2", "false", "[WPA2]")]
    [TestCase("WPA_WPA2", "true", "[WPA][WPA2][WPS]")]
    [TestCase("WPA2_WPA3", "", "[WPA2][WPA3]")]
    [TestCase("WEP", "false", "[WEP]")]
    [TestCase("OPEN", "false", "")]
    [TestCase("UNKNOWN", "", "")]
    public async Task SpellsOutWifiEncryptionAsWigleCapabilities(string encryption, string wps, string expected)
    {
        var observation = (await Import(Document(WifiColumns, WifiRow(encryption: encryption, wps: wps)))).Single();

        Assert.That(observation.AuthMode, Is.EqualTo(expected));
    }

    [Test]
    public async Task MarksRecordsWithoutASignalReadingAsUnknown()
    {
        var observation = (await Import(Document(WifiColumns, WifiRow(signal: "")))).Single();

        Assert.That(observation.RSSI, Is.EqualTo(-200));
    }

    [Test]
    public async Task ReadsBluetoothRecords()
    {
        var observation = (await Import(Document(BluetoothColumns, BluetoothRow()))).Single();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(observation.MAC, Is.EqualTo("02:00:00:00:00:02"));
            Assert.That(observation.SSID, Is.EqualTo("Test device"));
            Assert.That(observation.AuthMode, Is.EqualTo("DUAL;RANDOM"));
            Assert.That(observation.RSSI, Is.EqualTo(-65));
            Assert.That(observation.CurrentLatitude, Is.EqualTo(52.52));
        }
    }

    // Older versions left technology UNKNOWN even for BLE scan results, which still carry
    // advertisement data; newer ones mark them LE.
    [TestCase("UNKNOWN", "DUAL", "6", "0600000000000000", "BLE")]
    [TestCase("UNKNOWN", "DUAL", "", "", "BT")]
    [TestCase("", "", "", "", "BT")]
    [TestCase("LE", "DUAL", "", "", "BLE")]
    [TestCase("BR_EDR", "DUAL", "", "", "BT")]
    [TestCase("UNKNOWN", "LE", "", "", "BLE")]
    public async Task TellsBluetoothLowEnergyFromClassic(string technology, string supported, string companyId,
        string mfgData, string expected)
    {
        var observation = (await Import(Document(BluetoothColumns,
            BluetoothRow(technology, supported, companyId, mfgData)))).Single();

        Assert.That(observation.Type, Is.EqualTo(expected));
    }

    [TestCase("6", "6")]
    [TestCase("4c", "76")]
    [TestCase("", null)]
    public async Task ConvertsTheHexCompanyIdToWiglesDecimalManufacturerId(string companyId, string? expected)
    {
        var observation = (await Import(Document(BluetoothColumns, BluetoothRow(companyId: companyId)))).Single();

        Assert.That(observation.MfgrId, Is.EqualTo(expected));
    }

    [Test]
    public async Task ReadsLteRecords()
    {
        var observation = (await Import(Document(LteColumns, LteRow()))).Single();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(observation.Type, Is.EqualTo("LTE"));
            Assert.That(observation.MAC, Is.EqualTo("26201_100_1000000"));
            // The resolver knows no operators here, so the name the phone reported is used.
            Assert.That(observation.SSID, Is.EqualTo("Test Provider"));
            Assert.That(observation.AuthMode, Is.EqualTo("LTE;26201"));
            // RSRP, not the RSSI in signalStrength.
            Assert.That(observation.RSSI, Is.EqualTo(-117));
            Assert.That(observation.Channel, Is.EqualTo(100));
            Assert.That(observation.Frequency, Is.Null);
            Assert.That(observation.CurrentLatitude, Is.EqualTo(52.52));
        }
    }

    [TestCase("262", "1", "262-01", "26201_100_1000000")]
    [TestCase("310", "30", "310-030", "310030_100_1000000")]
    public async Task PrefersThePlmnColumnBecauseItKeepsTheMncDigits(string mcc, string mnc, string plmn, string expected)
    {
        var observation = (await Import(Document(LtePlmnColumns, LtePlmnRow(mcc, mnc, plmn)))).Single();

        Assert.That(observation.MAC, Is.EqualTo(expected));
    }

    private static IEnumerable<TestCaseData> OtherCellTechnologies()
    {
        yield return new TestCaseData(
            "deviceTime,latitude,longitude,altitude,speed,accuracy,missionId,recordNumber,groupNumber,mcc,mnc,lac,ci,arfcn,bsic,signalStrength,ta,servingCell,provider,slot,deviceSerialNumber,locationAge,plmn",
            Prefix + ",1,1,262,1,200,3000,60,12,-80.0,,true,Test Provider,,0000000000000000,1000,262-01",
            "GSM", "26201_200_3000", -80, 60).SetArgDisplayNames("GSM");
        yield return new TestCaseData(
            "deviceTime,latitude,longitude,altitude,speed,accuracy,missionId,recordNumber,groupNumber,mcc,mnc,lac,cid,uarfcn,psc,rscp,ecno,signalStrength,servingCell,provider,slot,deviceSerialNumber,locationAge,plmn",
            Prefix + ",1,1,262,1,200,4000,10700,300,-90.0,-8.0,-70.0,true,Test Provider,,0000000000000000,1000,262-01",
            "WCDMA", "26201_200_4000", -90, 10700).SetArgDisplayNames("UMTS");
        yield return new TestCaseData(
            "deviceTime,latitude,longitude,altitude,speed,accuracy,missionId,recordNumber,groupNumber,mcc,mnc,tac,nci,narfcn,pci,ssRsrp,ssRsrq,ssSinr,csiRsrp,csiRsrq,csiSinr,ta,servingCell,provider,slot,deviceSerialNumber,locationAge,plmn,connectionStatus,cellBandwidthsKhz",
            Prefix + ",1,1,262,1,100,68719476000,632628,500,-100.0,-11.0,5.0,,,,,true,Test Provider,,0000000000000000,1000,262-01,,",
            "NR", "26201_100_68719476000", -100, 632628).SetArgDisplayNames("NR");
        yield return new TestCaseData(
            "deviceTime,latitude,longitude,altitude,speed,accuracy,missionId,recordNumber,groupNumber,sid,nid,zone,bsid,channel,pnOffset,signalStrength,ecio,servingCell,provider,slot,deviceSerialNumber,locationAge",
            Prefix + ",1,1,4100,7,,1234,384,120,-85.0,-6.0,true,Test Provider,,0000000000000000,1000",
            "CDMA", "4100_7_1234", -85, 384).SetArgDisplayNames("CDMA");
    }

    [TestCaseSource(nameof(OtherCellTechnologies))]
    public async Task ReadsOtherCellTechnologies(string columns, string row, string type, string mac, int rssi, int channel)
    {
        var observation = (await Import(Document(columns, row))).Single();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(observation.Type, Is.EqualTo(type));
            Assert.That(observation.MAC, Is.EqualTo(mac));
            Assert.That(observation.RSSI, Is.EqualTo(rssi));
            Assert.That(observation.Channel, Is.EqualTo(channel));
            Assert.That(observation.SSID, Is.EqualTo("Test Provider"));
            Assert.That(observation.CurrentLatitude, Is.EqualTo(52.52));
        }
    }

    [Test]
    public async Task SkipsRecordsWithoutAPositionFixOrCellIdentity()
    {
        var observations = await Import(Document(LteColumns,
            LteRow(),
            LteRow(location: NoFixLocation, eci: "1000001"),
            // A neighbour cell reported by EARFCN and PCI alone.
            LteRow(eci: "")));

        Assert.That(observations.Select(observation => observation.MAC), Is.EqualTo(new[] { "26201_100_1000000" }));
    }

    [Test]
    public async Task FilesWithoutNetworkRecordsWarnAndImportNothing()
    {
        var observations = await Import(Document(
            "deviceTime,latitude,longitude,altitude,speed,accuracy,missionId,recordNumber,groupNumber,constellation,spaceVehicleId,carrierFreqHz,cn0DbHz",
            Prefix + ",1,1,GPS,5,1575420000,30.0"));

        Assert.That(observations, Is.Empty);
        Assert.That(logger.Warnings, Has.Count.EqualTo(1));
    }

    [Test]
    public async Task DetectsFilesWithoutTheCommentBlock()
    {
        using var reader = new StringReader(WifiColumns + "\r\n" + WifiRow() + "\r\n");
        var header = (await reader.ReadLineAsync())!;

        Assert.That(importer.Detect("text/csv", header), Is.True);
        Assert.That((await importer.Import(header, reader)).Single().MAC, Is.EqualTo("02:00:00:00:00:01"));
    }

    [Test]
    public void OtherCsvImportersDoNotClaimNetworkSurveyFiles()
    {
        ITextImport[] others = [new CsvWigleObservationsImport(), new CsvOpenCellIdImport(resolver), new CsvMylnikovCellImport(resolver)];

        Assert.That(others.Where(other => other.Detect("text/csv", Banner)), Is.Empty);
    }

    private async Task<Observation[]> Import(string document)
    {
        using var reader = new StringReader(document);
        var header = (await reader.ReadLineAsync())!;
        Assert.That(importer.Detect("text/csv", header), Is.True);
        return (await importer.Import(header, reader)).ToArray();
    }
}
