using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using System.Net.Mime;
using Wistellar.Core.Entities;
using Wistellar.Core.Import;
using Wistellar.Core.Services;
using Wistellar.Server.Attributes;
using Wistellar.Server.Config;
using Wistellar.Server.Models;
using Wistellar.Server.Services;

namespace Wistellar.Server.Controllers
{
    [ApiController]
    [Route("api/v2")]
    public class WiGleServerController(
        ILogger<WiGleServerController> logger,
        DatabaseService databaseService,
        StreamImporter streamImporter
        ) : ControllerBase
    {
        [HttpPost("activate")]
        [Consumes(MediaTypeNames.Application.FormUrlEncoded)]
        [EnableRateLimiting(ServiceConfiguration.OnlyOneConcurrencyPolicy)]
        public async Task<ApiTokenResponse> OnActivate(
            [FromForm(Name = "credential_0")] string login,
            [FromForm(Name = "credential_1")] string password,
            [FromForm(Name = "type")] string type,
            ILocalAuthenticationService auth)
        {
            var token = await auth.SignInAsync(login, password);

            return new ApiTokenResponse()
            {
                Success = true,
                AuthName = login,
                Token = token
            };
        }

        [HttpPost("file/upload")]
        [Authorize]
        [DisableFormValueModelBinding]
        [EnableRateLimiting(ServiceConfiguration.OnlyOneConcurrencyPolicy)]
        [RequestSizeLimit(200_073_741_824)]

        public async Task<UploadResponse> OnFileUpload(CancellationToken ct)
        {
            var results = await streamImporter.ImportFileStreamAsync([], "", Request.ContentType ?? "", Request.Body, ct);

            var rootFiles = results.Where(r => !r.FileName.Contains('/'));
            var result = new UploadResponse()
            {
                Observer = User.Identity?.Name ?? "Anonymous",
                Success = results.Any(r => r.Success),
                Warning = string.Join("\r\n", results.Select(r => r.Warning).Where(w => !string.IsNullOrWhiteSpace(w))),

                Results = new UploadResultsResponse()
                {
                    Filename = string.Join(",", rootFiles.Select(f => f.FileName)),
                    Filesize = rootFiles.Select(f => f.FileSize).Sum(),
                    TimeTaken = rootFiles.Select(f => f.TimeTaken).Sum().ToString(),
                    Transids = results.Select((f, i) => new TransidResponse()
                    {
                        file = f.FileName,
                        size = f.FileSize,
                        transId = i.ToString()
                    }).ToList()
                }
            };
            return result;
        }

        [HttpGet("file/transactions")]
        [EnableRateLimiting(ServiceConfiguration.OnlyOneConcurrencyPolicy)]
        [Authorize]
        public async Task<TranslogResponse> OnFileTransactions(
            [FromQuery] int pagestart,
            [FromQuery] int pageend
        )
        {
            if (pagestart == 0)
            {
                await databaseService.UpdateCalculatedColumns();
            }
            var result = new TranslogResponse()
            {
                success = true,
                geoQueueDepth = 0,
                processingQueueDepth = 0,
                trilaterationQueueDepth = 0,
                results = pagestart > 0 ? [] :
                [
                    new()
                    {
                        Username = User.Identities.FirstOrDefault()?.Name ?? "unknown",
                        Brand = "BRAND",
                        Model = "MODEL",
                        OsRelease = "OSRELEASE",
                        FileName = "upload.csv",
                        Transid = "1",
                        FirstTime = DateTime.UtcNow,
                        Lastupdt = DateTime.UtcNow,
                    }
                ]
            };
            return result;
        }

        [HttpGet("stats/user")]
        [Authorize]
        public async Task<IActionResult> OnStatsUser()
        {
            return Ok();
        }

        [HttpGet("bluetooth/search")]
        [Authorize]
        public async Task<BluetoothSearchResponse> OnBluetoothSearch(
       [FromQuery] string? name,
       [FromQuery] string? namelike,
       [FromQuery] string? netid,
       [FromQuery] double? latrange1,
       [FromQuery] double? latrange2,
       [FromQuery] double? longrange1,
       [FromQuery] double? longrange2,
       CancellationToken ct
       )
        {
            var nameFilter = name ?? namelike;
            var result = databaseService.GetNetworksAsync(new NetworkSearchFilter()
            {
                Types = ["B", "E"],
                SSID = nameFilter != null ? [nameFilter] : [],
                BSSID = netid != null ? [netid] : [],
                BestLat = new MinMaxRange<double?>(latrange1, latrange2),
                BestLon = new MinMaxRange<double?>(longrange1, longrange2),
            }, ct);

            var response = new BluetoothSearchResponse()
            {
                Success = true,
                Results = [],
            };
            await foreach (var network in result.WithCancellation(ct))
            {
                ct.ThrowIfCancellationRequested();
                response.TotalResults++;
                response.Results.Add(new BluetoothNetwork()
                {
                    Trilat = network.BestLatitude,
                    Trilong = network.BestLongitude,
                    Capabilities = [network.Capabilities],
                    Firsttime = DateTime.FromFileTimeUtc(network.LastSeen).Subtract(TimeSpan.FromSeconds(network.Dwell ?? 0)),
                    Lasttime = DateTime.FromFileTimeUtc(network.LastSeen),
                    Name = network.SSID,
                    Netid = network.BSSID,
                    Ssid = network.SSID,
                    Type = network.Type,
                    Lastupdt = DateTime.FromFileTimeUtc(network.LastSeen),
                    Transid = "0",
                });
            }
            logger?.LogInformation("Getting networks DONE");

            return response;
        }

        [HttpGet("network/search")]
        [Authorize]
        public async Task<IActionResult> OnNetworkSearch(
            [FromQuery] string? ssid,
            [FromQuery] string? ssidlike,
            [FromQuery] string? netid,
            [FromQuery] string? encryption, // WEP | ...
            [FromQuery] double? latrange1,
            [FromQuery] double? latrange2,
            [FromQuery] double? longrange1,
            [FromQuery] double? longrange2,
             CancellationToken ct
            )
        {
            var ssidFilter = ssid ?? ssidlike;
            var result = databaseService.GetNetworksAsync(new NetworkSearchFilter()
            {
                Types = ["W"],
                SSID = ssidFilter != null ? [ssidFilter] : [],
                BSSID = netid != null ? [netid] : [],
                BestLat = new MinMaxRange<double?>(latrange1, latrange2),
                BestLon = new MinMaxRange<double?>(longrange1, longrange2),
            }, ct);

            var response = new BluetoothSearchResponse()
            {
                Success = true,
                Results = [],
            };
            await foreach (var network in result.WithCancellation(ct))
            {
                ct.ThrowIfCancellationRequested();
                response.TotalResults++;
                response.Results.Add(new BluetoothNetwork()
                {
                    Trilat = network.BestLatitude,
                    Trilong = network.BestLongitude,
                    Capabilities = [network.Capabilities],
                    Firsttime = DateTime.FromFileTimeUtc(network.LastSeen).Subtract(TimeSpan.FromSeconds(network.Dwell ?? 0)),
                    Lasttime = DateTime.FromFileTimeUtc(network.LastSeen),
                    Name = network.SSID,
                    Netid = network.BSSID,
                    Ssid = network.SSID,
                    Type = network.Type,
                    Lastupdt = DateTime.FromFileTimeUtc(network.LastSeen),
                    Transid = "0",
                });
            }
            logger?.LogInformation("Getting networks DONE");
            return Ok();
        }

        [HttpGet("news/latest")]
        public async Task<WiGLENews> OnNewsLatest()
        {
            var welcomeArticle = new NewsItem
            {
                Subject = "Welcome to Wistellar - Your Private Wardriving Hub!",
                Story = @"[b]Welcome to Wistellar![/b]

📡 Wistellar is an [b]open-source[/b], self-hosted server for exploring wireless networks.",
                UserName = "Wistellar",
                PostDate = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss"),
                Link = "https://github.com/elebree/wistellar",
                StoryId = "welcome_1",
                More = false
            };

            return new WiGLENews()
            {
                Success = true,
                Results = [welcomeArticle],
            };
        }

        /// <summary>
        /// Import my observed database from server
        /// </summary>
        /// <returns></returns>
        [HttpGet("network/mine")]
        [Authorize]
        public async Task<IActionResult> OnNetworkMine()
        {
            return Ok();
        }
    }

    public enum Status
    {
        Pending,
        Completed,
        Failed
    }
}
