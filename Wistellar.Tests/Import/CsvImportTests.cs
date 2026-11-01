using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using Wistellar.Core;
using Wistellar.Core.Services.MobileNetwork;
using Wistellar.Tests.Support;

namespace Wistellar.Tests.Import;

[TestFixture]
[Category("Unit")]
public sealed class CsvImportTests
{
    private WiGleBackupContext lookupContext = null!;
    private MobileNetworkResolverService resolver = null!;

    [SetUp]
    public void SetUp()
    {
        // Lookup uses an empty in-memory dictionary; hosted startup and downloads are never run.
        lookupContext = new WiGleBackupContext();
        resolver = new MobileNetworkResolverService(new MccMncFetchService(NullLoggerFactory.Instance),
            new MccMncDbService(NullLoggerFactory.Instance, lookupContext), NullLoggerFactory.Instance);
    }

    [TearDown]
    public void TearDown() => lookupContext.Dispose();

    [TestCase("wigle", true)]
    [TestCase("opencellid", true)]
    [TestCase("opencellid", false)]
    [TestCase("mylnikov", true)]
    public async Task ReadsAllRecordsWithoutSynchronousStreamOperations(string format, bool includeHeader)
    {
        var data = Encoding.UTF8.GetBytes(CsvFixture.Document(format, includeHeader: includeHeader));
        using var input = new AsyncOnlyReadStream(data);
        using var reader = new StreamReader(input);
        var header = (await reader.ReadLineAsync())!;
        var importer = CsvFixture.Importer(format, resolver);

        Assert.That(importer.Detect("text/csv", header), Is.True);
        var observations = (await importer.Import(header, reader)).ToArray();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(observations, Has.Length.EqualTo(CsvFixture.UploadRowCount));
            Assert.That(observations.Select(observation => observation.MAC).Distinct().Count(), Is.EqualTo(CsvFixture.UploadRowCount));
            Assert.That(observations[0].CurrentLatitude, Is.EqualTo(52.52));
            Assert.That(observations[0].CurrentLongitude, Is.EqualTo(13.405));
            Assert.That(observations[0].MAC, Is.EqualTo(format == "wigle" ? "02:00:00:00:00:00" : "26201_100_1000000"));
            Assert.That(observations[^1].MAC, Is.EqualTo(format == "wigle" ? "02:00:00:00:03:FF" : "26201_100_1001023"));
            Assert.That(observations[0].Type, Is.EqualTo(format switch { "wigle" => "WIFI", "mylnikov" => "WCDMA", _ => "LTE" }));
        }
    }

    [TestCase("wigle")]
    [TestCase("opencellid")]
    [TestCase("mylnikov")]
    public async Task ReturnedObservationsDoNotDependOnAnOpenReader(string format)
    {
        using var reader = new StringReader(CsvFixture.Document(format, rowCount: 3));
        var header = (await reader.ReadLineAsync())!;
        var importer = CsvFixture.Importer(format, resolver);

        var observations = await importer.Import(header, reader);
        reader.Dispose();

        Assert.That(observations.Count(), Is.EqualTo(3));
    }

    [TestCase("wigle")]
    [TestCase("opencellid")]
    [TestCase("mylnikov")]
    public async Task HeaderOnlyFilesProduceNoObservations(string format)
    {
        using var reader = new StringReader(CsvFixture.Document(format, rowCount: 0));
        var header = (await reader.ReadLineAsync())!;

        var observations = await CsvFixture.Importer(format, resolver).Import(header, reader);

        Assert.That(observations, Is.Empty);
    }

    [TestCase("wigle")]
    [TestCase("opencellid")]
    [TestCase("mylnikov")]
    public async Task HonorsCancellationBeforeReturningRecords(string format)
    {
        using var reader = new StringReader(CsvFixture.Document(format, rowCount: 3));
        var header = (await reader.ReadLineAsync())!;
        var importer = CsvFixture.Importer(format, resolver);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.CatchAsync<OperationCanceledException>(() => importer.Import(header, reader, cancellation.Token));
    }
}
