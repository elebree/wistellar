using System.IO.Compression;
using System.Text;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Wistellar.Core;
using Wistellar.Core.Import;
using Wistellar.Core.Models;
using Wistellar.Core.Services;
using Wistellar.Core.Services.MobileNetwork;
using Wistellar.Server.Import;
using Wistellar.Tests.Support;

namespace Wistellar.Tests.Import;

[TestFixture]
[Category("Integration")]
public sealed class StreamImporterTests
{
    private string directory = "";
    private WiGleBackupContext context = null!;
    private ServiceProvider services = null!;
    private RecordingLogger<StreamImporter> logger = null!;
    private StreamImporter importer = null!;

    [SetUp]
    public async Task SetUp()
    {
        directory = Directory.CreateTempSubdirectory("wistellar-tests-").FullName;
        var databasePath = Path.Combine(directory, "observations.sqlite");
        context = new WiGleBackupContext(databasePath);
        // Avoid pooled connections retaining temporary files or affecting other fixtures.
        context.Database.SetConnectionString(new SqliteConnectionStringBuilder
        {
            DataSource = databasePath,
            Pooling = false,
        }.ToString());
        await context.Database.MigrateAsync();
        var resolver = new MobileNetworkResolverService(new MccMncFetchService(NullLoggerFactory.Instance),
            new MccMncDbService(NullLoggerFactory.Instance, context), NullLoggerFactory.Instance);
        services = new ServiceCollection()
            .AddSingleton<ITextImport>(new CsvOpenCellIdImport(resolver))
            .AddSingleton<ITextImport>(new CsvMylnikovCellImport(resolver))
            .AddSingleton<ITextImport>(new CsvWigleObservationsImport())
            .AddSingleton<ITextImport>(new CsvNetworkSurveyImport(NullLogger<CsvNetworkSurveyImport>.Instance, resolver))
            .BuildServiceProvider();
        logger = new RecordingLogger<StreamImporter>();
        importer = new StreamImporter(logger, new DatabaseService(NullLoggerFactory.Instance, context), services);
    }

    [TearDown]
    public async Task TearDown()
    {
        if (context is not null) await context.DisposeAsync();
        services?.Dispose();
        if (Directory.Exists(directory))
        {
            foreach (var file in Directory.EnumerateFiles(directory)) File.Delete(file);
            Directory.Delete(directory);
        }
    }

    [TestCase(".kml")]
    [TestCase(".kml.gz")]
    [TestCase(".zip")]
    [TestCase(".kmz")]
    [TestCase(".KMZ")]
    public async Task ImportsMultipartFilesAndPersistsObservations(string extension)
    {
        var kml = ArchiveFixture.Kml();
        var data = extension switch
        {
            ".kml" => kml,
            ".kml.gz" => ArchiveFixture.Gzip(kml),
            _ => ArchiveFixture.Zip(CompressionLevel.Optimal, ("files/survey.KML", kml)),
        };

        var results = await ImportUpload("survey" + extension, data);

        AssertSuccessful(results);
        await AssertPersisted();
    }

    [TestCase("wigle", false, true)]
    [TestCase("wigle", true, true)]
    [TestCase("opencellid", false, true)]
    [TestCase("opencellid", true, true)]
    [TestCase("opencellid", false, false)]
    [TestCase("opencellid", true, false)]
    [TestCase("mylnikov", false, true)]
    [TestCase("mylnikov", true, true)]
    [TestCase("networksurvey-wifi", false, true)]
    [TestCase("networksurvey-wifi", true, true)]
    [TestCase("networksurvey-lte", false, true)]
    [TestCase("networksurvey-lte", true, true)]
    public async Task ImportsLargeCsvUploadsWithoutSynchronousRequestReads(string format, bool gzip, bool includeHeader)
    {
        var data = Encoding.UTF8.GetBytes(CsvFixture.Document(format, includeHeader: includeHeader));
        if (gzip) data = ArchiveFixture.Gzip(data);
        Assert.That(data.Length, Is.GreaterThan(8192), "The request must exceed the input buffers to exercise later reads.");

        var results = await ImportUpload("survey.csv" + (gzip ? ".gz" : ""), data);

        AssertSuccessful(results);
        Assert.That(await context.Networks.CountAsync(), Is.EqualTo(CsvFixture.UploadRowCount));
        Assert.That(await context.Locations.CountAsync(), Is.EqualTo(CsvFixture.UploadRowCount));
        var firstId = CsvFixture.IsWifi(format) ? "02:00:00:00:00:00" : "26201_100_1000000";
        var lastId = CsvFixture.IsWifi(format) ? "02:00:00:00:03:FF" : "26201_100_1001023";
        Assert.That(await context.Networks.AnyAsync(network => network.BSSID == firstId), Is.True);
        Assert.That(await context.Networks.AnyAsync(network => network.BSSID == lastId), Is.True);
    }

    [Test]
    public async Task ImportsZippedNetworkSurveyLogsOfEachTechnology()
    {
        var data = ArchiveFixture.Zip(CompressionLevel.Optimal,
            ("craxiom-bluetooth-20260329-195821.csv", Encoding.UTF8.GetBytes(
                NetworkSurveyFixture.Document(NetworkSurveyFixture.BluetoothColumns, NetworkSurveyFixture.BluetoothRow()))),
            ("craxiom-lte-20260329-195823.csv", Encoding.UTF8.GetBytes(
                NetworkSurveyFixture.Document(NetworkSurveyFixture.LteColumns, NetworkSurveyFixture.LteRow()))),
            ("craxiom-wifi-20260329-195826.csv", Encoding.UTF8.GetBytes(
                NetworkSurveyFixture.Document(NetworkSurveyFixture.WifiColumns, NetworkSurveyFixture.WifiRow()))));

        var results = await ImportUpload("survey.zip", data);

        AssertSuccessful(results);
        var networks = await context.Networks.OrderBy(network => network.BSSID).ToListAsync();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(networks.Select(network => (network.BSSID, network.Type, network.Capabilities, network.BestLevel)),
                Is.EqualTo(new[]
                {
                    ("02:00:00:00:00:01", "W", "[WPA3]", -70),
                    ("02:00:00:00:00:02", "E", "DUAL;RANDOM", -65),
                    ("26201_100_1000000", "L", "LTE;26201", -117),
                }));
            Assert.That(networks.Select(network => (network.BestLatitude, network.BestLongitude)), Is.All.EqualTo((52.52, 13.405)));
            // 2026-03-29T19:58:26+02:00, stored as UTC milliseconds.
            Assert.That(networks[0].LastSeen, Is.EqualTo(1774807106000));
            Assert.That(await context.Locations.CountAsync(), Is.EqualTo(3));
        }
    }

    [TestCase(CompressionLevel.NoCompression)]
    [TestCase(CompressionLevel.Optimal)]
    public async Task KmzSkipsDirectoriesAndResourcesAndUsesOnlyAsyncRequestReads(CompressionLevel compression)
    {
        // This exceeds MultipartReader's buffer, so a synchronous ZipArchive constructor fails.
        var data = ArchiveFixture.Zip(compression,
            ("images/", []),
            ("images/icon.png", ArchiveFixture.ResourceLargerThanMultipartBuffer()),
            ("files/doc.KML", ArchiveFixture.Kml()));

        var results = await ImportUpload("survey.kmz", data);

        AssertSuccessful(results);
        Assert.That(results.Any(result => result.FileName.EndsWith("images/icon.png", StringComparison.Ordinal)), Is.False);
        await AssertPersisted();
    }

    [Test]
    public async Task RawKmzUsesItsContentTypeAndDoesNotRequireAFilenameOrSynchronousReads()
    {
        var data = ArchiveFixture.Zip(CompressionLevel.Optimal, ("doc.kml", ArchiveFixture.Kml()));
        using var input = new AsyncOnlyReadStream(data);

        var results = await importer.ImportFileStreamAsync([], "", "application/vnd.google-earth.kmz", input,
            CancellationToken.None);

        AssertSuccessful(results);
        await AssertPersisted();
    }

    [Test]
    public async Task ImportsGzipWrappedKmz()
    {
        var kmz = ArchiveFixture.Zip(CompressionLevel.Optimal, ("doc.kml", ArchiveFixture.Kml()));

        var results = await ImportUpload("survey.kmz.gz", ArchiveFixture.Gzip(kmz));

        AssertSuccessful(results);
        await AssertPersisted();
    }

    [Test]
    public async Task PersistsUnknownSignalAndManufacturerWhenKmzContainsEmptyRssi()
    {
        var description = string.Concat(KmlFixture.Description.Split('\n').Select(line => line.Trim()))
            .Replace("<b>High GPS RSSI: </b>-55", "<b>High GPS RSSI: </b>")
            .Replace("Test manufacturer", "Unknown Manufacturer");
        var data = ArchiveFixture.Zip(CompressionLevel.Optimal, ("doc.kml", ArchiveFixture.Kml(description)));

        var results = await ImportUpload("survey.kmz", data);

        Assert.That(results.Any(result => result.Success), Is.True);
        await AssertPersisted(expectedSignal: -200);
        var observation = await context.Observations.SingleAsync();
        Assert.That(observation.MfgrId, Is.EqualTo("Unknown Manufacturer"));
        Assert.That(logger.Warnings, Has.Count.EqualTo(1));
        Assert.That(logger.Warnings.Single(), Does.Contain("unknown RSSI").And.Contain("doc.kml"));
    }

    [TestCase("empty.kmz", "KMZ archive contains no KML files")]
    [TestCase("empty.zip", "Archive contains no importable files")]
    public async Task EmptyArchivesReportNoImport(string fileName, string expectedWarning)
    {
        var results = await ImportUpload(fileName, ArchiveFixture.Zip(CompressionLevel.Optimal));

        Assert.That(results.Any(result => result.Success), Is.False);
        Assert.That(results.Any(result => result.Warning == expectedWarning), Is.True);
        Assert.That(await context.Networks.CountAsync(), Is.Zero);
        Assert.That(await context.Locations.CountAsync(), Is.Zero);
    }

    [Test]
    public async Task KmzContainingOnlyResourcesReportsMissingKml()
    {
        var data = ArchiveFixture.Zip(CompressionLevel.Optimal, ("images/icon.png", new byte[] { 1, 2, 3 }));

        var results = await ImportUpload("resources.kmz", data);

        Assert.That(results.Any(result => result.Success), Is.False);
        Assert.That(results.Any(result => result.Warning == "KMZ archive contains no KML files"), Is.True);
        Assert.That(await context.Networks.CountAsync(), Is.Zero);
    }

    [Test]
    public async Task UnknownZipEntriesWarnWithoutPreventingKmlImport()
    {
        var data = ArchiveFixture.Zip(CompressionLevel.Optimal,
            ("metadata.unknown", new byte[] { 1, 2, 3 }),
            ("doc.kml", ArchiveFixture.Kml()));

        var results = await ImportUpload("survey.zip", data);

        Assert.That(results.Any(result => result.Success), Is.True);
        Assert.That(results.Any(result => result.Warning == "Unsupported file type"), Is.True);
        await AssertPersisted();
    }

    [Test]
    public async Task CancellationStopsArchiveImportBeforeWritingObservations()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var data = ArchiveFixture.Zip(CompressionLevel.Optimal, ("doc.kml", ArchiveFixture.Kml()));

        await Assert.CatchAsync<OperationCanceledException>(() => ImportUpload("survey.kmz", data, cancellation.Token));

        Assert.That(await context.Networks.CountAsync(), Is.Zero);
        Assert.That(await context.Locations.CountAsync(), Is.Zero);
    }

    private async Task<List<ImportResult>> ImportUpload(string fileName, byte[] data,
        CancellationToken cancellationToken = default)
    {
        using var form = new MultipartFormDataContent();
        form.Add(new ByteArrayContent(data), "file", fileName);
        using var input = new AsyncOnlyReadStream(await form.ReadAsByteArrayAsync());
        return await importer.ImportFileStreamAsync([], "", form.Headers.ContentType!.ToString(), input,
            cancellationToken);
    }

    private void AssertSuccessful(List<ImportResult> results)
    {
        Assert.That(results.Any(result => result.Success), Is.True);
        Assert.That(results.Select(result => result.Warning), Is.All.Empty);
        Assert.That(logger.Warnings, Is.Empty);
    }

    private async Task AssertPersisted(int expectedSignal = -55)
    {
        var network = await context.Networks.SingleAsync();
        var location = await context.Locations.SingleAsync();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(network.BSSID, Is.EqualTo("02:00:00:00:00:01"));
            Assert.That(network.SSID, Is.EqualTo("Test network"));
            Assert.That(network.BestLevel, Is.EqualTo(expectedSignal));
            Assert.That(location.Level, Is.EqualTo(expectedSignal));
            Assert.That(location.Lat, Is.EqualTo(52.52));
            Assert.That(location.Lon, Is.EqualTo(13.405));
        }
    }
}
