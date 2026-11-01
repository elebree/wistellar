using System.IO.Compression;
using System.Text;

namespace Wistellar.Tests.Support;

internal static class ArchiveFixture
{
    public static byte[] Kml(string description = KmlFixture.Description) =>
        Encoding.UTF8.GetBytes(KmlFixture.DocumentWithDescription(description));

    public static byte[] Zip(CompressionLevel level, params (string Name, byte[] Data)[] files)
    {
        using var output = new MemoryStream();
        using (var archive = new ZipArchive(output, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var file in files)
            {
                using var entry = archive.CreateEntry(file.Name, level).Open();
                entry.Write(file.Data);
            }
        }
        return output.ToArray();
    }

    public static byte[] Gzip(byte[] data)
    {
        using var output = new MemoryStream();
        using (var gzip = new GZipStream(output, CompressionLevel.Optimal, leaveOpen: true)) gzip.Write(data);
        return output.ToArray();
    }

    public static byte[] ResourceLargerThanMultipartBuffer()
    {
        var bytes = new byte[64 * 1024];
        new Random(42).NextBytes(bytes);
        return bytes;
    }
}
