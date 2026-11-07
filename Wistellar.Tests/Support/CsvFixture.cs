using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using Wistellar.Core.Import;
using Wistellar.Core.Services.MobileNetwork;
using Wistellar.Server.Import;

namespace Wistellar.Tests.Support;

internal static class CsvFixture
{
    public const int UploadRowCount = 1024;

    /// <summary>Wi-Fi formats are keyed by BSSID; the others by a synthetic operator_area_cell ID.</summary>
    public static bool IsWifi(string format) => format is "wigle" or "networksurvey-wifi";

    public static ITextImport Importer(string format, MobileNetworkResolverService resolver) => format switch
    {
        "wigle" => new CsvWigleObservationsImport(),
        "opencellid" => new CsvOpenCellIdImport(resolver),
        "mylnikov" => new CsvMylnikovCellImport(resolver),
        "networksurvey-wifi" or "networksurvey-lte" => new CsvNetworkSurveyImport(NullLogger<CsvNetworkSurveyImport>.Instance, resolver),
        _ => throw new ArgumentException($"Unknown CSV format: {format}", nameof(format)),
    };

    public static string Document(string format, int rowCount = UploadRowCount, bool includeHeader = true)
    {
        var output = new StringBuilder();
        var random = new Random(42);
        if (includeHeader)
        {
            output.AppendLine(format switch
            {
                "wigle" => "WigleWifi-1.6,appRelease=1.0\nMAC,SSID,AuthMode,FirstSeen,Channel,Frequency,RSSI,CurrentLatitude,CurrentLongitude,AltitudeMeters,AccuracyMeters,RCOIs,MfgrId,Type",
                "opencellid" => "radio,mcc,net,area,cell,unit,lon,lat,range,samples,changeable,created,updated,averageSignal",
                "mylnikov" => "id,data_source,radio_type,mcc,mnc,lac,cellid,lat,lon,range,created,updated",
                "networksurvey-wifi" => $"{NetworkSurveyFixture.Banner}\n# CSV Version=0.4.0\n{NetworkSurveyFixture.WifiColumns}",
                "networksurvey-lte" => $"{NetworkSurveyFixture.Banner}\n# CSV Version=0.6.0\n{NetworkSurveyFixture.LtePlmnColumns}",
                _ => throw new ArgumentException($"Unknown CSV format: {format}", nameof(format)),
            });
        }

        for (var i = 0; i < rowCount; i++)
        {
            // Vary unused metadata as well as identifiers so GZip cannot shrink the entire
            // request into the multipart/DeflateStream buffers and mask synchronous reads.
            var row = format switch
            {
                "wigle" => $"02:00:00:{(i >> 16) & 255:X2}:{(i >> 8) & 255:X2}:{i & 255:X2},Test network {i}-{Convert.ToHexString(RandomBytes(random))},[WPA2-PSK-CCMP][ESS],2026-10-25 12:00:00,1,2412,-55,52.52,13.405,34,5,,,WIFI",
                "opencellid" => FormattableString.Invariant($"LTE,262,1,100,{1_000_000 + i},{random.Next(0, 503)},13.405,52.52,{random.Next(100, 1_000_000)},{random.Next(1, 100_000)},1,{random.Next(1_600_000_000, 1_790_000_000)},1792929600,-55"),
                "mylnikov" => FormattableString.Invariant($"{random.NextInt64(1, long.MaxValue)},-1,WCDM,262,1,100,{1_000_000 + i},52.52,13.405,{random.Next(100, 1_000_000)},{random.Next(1_600_000_000, 1_790_000_000)},1792929600"),
                // Wi-Fi uses a decimal comma and LTE a decimal point, as phones in different locales write them.
                "networksurvey-wifi" => $"2026-10-25T12:00:00.000+02:00,\"52,520000\",\"13,405000\",\"34,5\",0,5,NS {Convert.ToHexString(RandomBytes(random))},{i + 1},,,02:00:00:{(i >> 16) & 255:X2}:{(i >> 8) & 255:X2}:{i & 255:X2},,,Test network {i},,,,,WPA2,false,1,2412,-55.0,,,IEEE80211AX,,MHZ_20,0000000000000000,1000",
                "networksurvey-lte" => FormattableString.Invariant($"2026-10-25T10:00:00.000Z,52.520000,13.405000,34.5,0,5,NS {Convert.ToHexString(RandomBytes(random))},{i + 1},{i},262,1,100,{1_000_000 + i},6300,{random.Next(0, 504)},-95.0,-10.0,,false,MHZ_20,Test Provider,-60.0,,1,3.0,0000000000000000,1000,262-01,,"),
                _ => throw new ArgumentException($"Unknown CSV format: {format}", nameof(format)),
            };
            output.AppendLine(row);
        }
        return output.ToString();
    }

    private static byte[] RandomBytes(Random random)
    {
        var bytes = new byte[32];
        random.NextBytes(bytes);
        return bytes;
    }
}
