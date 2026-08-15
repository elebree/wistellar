using Microsoft.AspNetCore.StaticFiles;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Net.Http.Headers;
using System.Diagnostics;
using System.IO.Compression;
using System.Net.Mime;
using Wistellar.Core.Models;
using Wistellar.Core.Services;

namespace Wistellar.Core.Import
{
    public class StreamImporter(
         ILogger<StreamImporter> logger,
         DatabaseService databaseService,
         IServiceProvider serviceProvider
    )
    {
        /// <summary>
        /// Imports one uploaded stream, recursing through multipart, gzip and zip wrappers until it
        /// reaches something it recognises. <paramref name="path"/> accumulates the names of the
        /// containers walked through so results can be reported against the original upload.
        /// </summary>
        public async Task<List<ImportResult>> ImportFileStreamAsync(string[] path, string fileName, string contentType, Stream stream, CancellationToken ct)
        {
            IList<ImportResult> result = [];
            var timer = new Stopwatch();
            string warning = "";
            timer.Start();
            var ext = Path.GetExtension(fileName);
            bool success = false;

            if (contentType.StartsWith(MediaTypeNames.Multipart.FormData + ";"))
            {
                var boundary = MultipartRequestHelper.GetBoundary(MediaTypeHeaderValue.Parse(contentType), int.MaxValue);
                var reader = new MultipartReader(boundary, stream);
                MultipartSection? section;
                while ((section = await reader.ReadNextSectionAsync(ct)) != null)
                {
                    var hasContentDispositionHeader =
                        ContentDispositionHeaderValue.TryParse(section.ContentDisposition, out var contentDisposition);

                    if (hasContentDispositionHeader && contentDisposition!.DispositionType.Equals("form-data") &&
                        !string.IsNullOrEmpty(contentDisposition.FileName.Value))
                    {
                        var fileName1 = contentDisposition.FileName.Value;
                        var contentType1 = GetContentType(fileName1);
                        await using var stream1 = section.Body;
                        result = [
                            .. result,
                    .. await ImportFileStreamAsync([], fileName1, contentType1, stream1, ct)];
                    }

                }
                success = result.All(r => r.Success);
            }
            else if (contentType == "application/x-gzip" || ext == ".gz")
            {
                // Decompress GZip stream
                await using var gzipStream = new GZipStream(stream, CompressionMode.Decompress, leaveOpen: true);
                var innerFileName = Path.GetFileNameWithoutExtension(fileName); // better than Split
                result = await ImportFileStreamAsync(
                    [.. path, fileName],
                    innerFileName,
                    GetContentType(innerFileName),
                    gzipStream,
                    ct
                );
                success = result.All(r => r.Success);
            }
            else if (
                contentType == MediaTypeNames.Application.Zip ||
                contentType == "application/x-zip-compressed" ||
                ext == ".zip")
            {
                using var archive = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: true);
                foreach (var entry in archive.Entries)
                {
                    if (string.IsNullOrEmpty(entry.Name)) // Skip directories
                        continue;

                    await using var entryStream = entry.Open(); // stream-based, memory-efficient
                    var innerFileName = entry.FullName;

                    result = [
                        ..result,
                        ..await ImportFileStreamAsync(
                            [.. path, fileName],
                            innerFileName,
                            GetContentType(innerFileName),
                            entryStream,
                            ct
                        )
                    ];
                }

                success = result.All(r => r.Success);
            }
            else if (contentType == "application/vnd.google-earth.kml+xml" || ext == ".kml")
            {
                success = await ImportKml(stream, fileName);
            }
            else if (contentType == MediaTypeNames.Text.Csv || ext == ".csv")
            {
                using var reader = new StreamReader(stream);
                var header = await reader.ReadLineAsync(ct);

                IEnumerable<Observation>? observations = null;

                // Every CSV format is identified from its first line, so an empty file cannot be
                // dispatched to an importer at all.
                if (header != null)
                {
                    foreach (var importer in serviceProvider.GetServices<ITextImport>())
                    {
                        if (importer.Detect(contentType, header))
                        {
                            logger.LogInformation("Import using {importer}", importer.Name);
                            observations = await importer.Import(header, reader);
                            break;
                        }
                    }
                }

                if (observations == null)
                {
                    warning = "Invalid content (no observations)";
                }
                else
                {
                    await databaseService.AddObservations(observations.ToArray());
                    success = true;
                }
            }
            else if (fileName.Split(".").Last() == "sqlite")
            {
                // Stage the upload under a unique name. The previous fixed name in the process
                // working directory collided between concurrent uploads and was never cleaned up.
                var stagedPath = Path.Combine(Path.GetTempPath(), $"wistellar-import-{Guid.NewGuid():N}.sqlite");
                try
                {
                    await using (var outputStream = File.Create(stagedPath))
                    {
                        await stream.CopyToAsync(outputStream, ct);
                    }

                    await databaseService.AddObservationsFromDb(stagedPath);
                    success = true;
                }
                finally
                {
                    // SQLite may leave -wal/-shm sidecars next to an attached database.
                    foreach (var suffix in new[] { "", "-wal", "-shm" })
                        File.Delete(stagedPath + suffix);
                }
            }
            else
            {
                warning = "Unsupported file type";
            }

            timer.Stop();
            return [
                new ImportResult
            {
                FileName = string.Join("/", [.. path, fileName]),
                FileSize = stream.CanSeek ? stream.Length:0,
                TimeTaken = timer.ElapsedMilliseconds,
                Success = success,
                Warning = warning,
            },
            ..result
                ];
        }

        private async Task<bool> ImportKml(Stream stream, string fileName)
        {
            var importer = new WifiDbImport(logger);
            using var reader = new StreamReader(stream);
            var observations = await importer.Import(fileName, reader);
            await databaseService.AddObservations(observations.ToArray());
            return true;
        }

        private static string GetContentType(string fileName)
        {
            new FileExtensionContentTypeProvider().TryGetContentType(fileName, out string? contentType);
            return contentType;

        }
    }
}
