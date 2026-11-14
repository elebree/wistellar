using HtmlAgilityPack;
using Microsoft.Extensions.Logging;
using System.Globalization;
using System.Text;
using System.Xml.Linq;
using Wistellar.Core.Models;

namespace Wistellar.Core.Import
{
    public class WifiDbImport(ILogger logger)
    {
        // Match the other importers' marker for unavailable signal strength.
        private const int UnknownRssi = -200;

        private static readonly Dictionary<string, string> AuthenticationMap = new(StringComparer.OrdinalIgnoreCase)
        {
            { "Otevýen\u0082" ,"ESS"},
            { "Otwarte" ,"ESS"},
            { "Ouvrir" ,"ESS"},
            { "Abierta" ,"ESS"},
            { "Aperta" ,"ESS"},
            { "Offen" ,"ESS"},
            { "Open" ,"ESS"},
            { "Shared Key", "WEP" },
            { "CCMP" ,"CCMP"},
            { "OWE" ,"RSN-OWE-CCMP][ESS"},
            { "TKIP" ,"TKIP"},
            { "Firmenweiter WPA" ,"WPA-EAP"},
            { "WPA-Enterprise" ,"WPA-EAP"},
            { "WPA-Entreprise" ,"WPA-EAP"},
            { "WPA-PSK" ,"WPA-PSK"},
            { "WPA" ,"WPA-PSK"},
            { "WPA-Personal" ,"WPA-PSK"},
            { "WPA-Personnel" ,"WPA-PSK"},
            { "WPA2" ,"WPA2-PSK"},
            { "WPA2-PSK" ,"WPA2-PSK"},
            { "WPA2-Personal" ,"WPA2-PSK"},
            { "WPA2-osobn¡" ,"WPA2-PSK"},
            { "WPA2ÿ-ÿPersonnel" ,"WPA2-PSK"},
            { "WPA2ÿ-ÿEntreprise" ,"WPA2-EAP"},
            { "WPA2-Enterprise" ,"WPA2-EAP"},
            { "WPA2_Enterprise" ,"WPA2-EAP"},
            { "WPA3-Personal" ,"WPA3-PSK"},
        };

        private static readonly Dictionary<string, string> EncryptionMap = new(StringComparer.OrdinalIgnoreCase)
        {
            { "TKIP","TKIP" },
            { "WEP","WEP" },
            { "CCMP","CCMP" },
            { "GCMP","GCMP" },
            { "None","NONE" },
            { "Nessuno","NONE" },
            { "Aucun","NONE" },
            { "Ninguna","NONE" },
            { "Brak","NONE" },
            { "Keine","NONE" },
            { "Unencrypted","NONE" },
            { "AES","CCMP" },
        };

        public string Name => nameof(WifiDbImport);

        public async Task<IEnumerable<Observation>> Import(string fileName, TextReader reader)
        {
            XDocument doc = await XDocument.LoadAsync(reader, new LoadOptions(), CancellationToken.None);
            XNamespace ns = "http://www.opengis.net/kml/2.2";

            var placemarks = doc.Descendants(ns + "Placemark");
            var observations = new List<Observation>();
            var unknownSignalCount = 0;

            foreach (var placemark in placemarks)
            {
                var descriptionText = placemark.Element(ns + "description")?.Value;
                if (descriptionText == null)
                    continue;

                var coordinates = placemark.Element(ns + "Point")?
                    .Element(ns + "coordinates")?
                    .Value?
                    .Split(",")?
                    .Select(v => double.Parse(v.Trim() != "" ? v.Trim() : "0", CultureInfo.InvariantCulture))?
                    .ToArray();

                var description = new HtmlDocument();
                description.LoadHtml(descriptionText);

                // Placemarks without a usable position carry no observation worth importing.
                if (coordinates == null || coordinates.Length != 3 || (coordinates[0] == 0 && coordinates[1] == 0))
                    continue;

                var ssid = description.DocumentNode?.SelectSingleNode($"//a")?.InnerText?.Trim() ?? "";
                ssid = ssid == "[Blank SSID]" ? "" : ssid;
                var authMode = ExtractValue(description, "Authentication");
                var encMode = ExtractValue(description, "Encryption");

                if (!AuthenticationMap.ContainsKey(authMode))
                    logger.LogWarning("Failed to convert Authentication {authMode} ({fileName})", authMode, fileName);

                if (!EncryptionMap.ContainsKey(encMode))
                    logger.LogWarning("Failed to convert Encryption {encMode} ({fileName})", encMode, fileName);

                if (!int.TryParse(ExtractValue(description, "High GPS RSSI"), NumberStyles.Integer,
                    CultureInfo.InvariantCulture, out var rssi))
                {
                    rssi = UnknownRssi;
                    unknownSignalCount++;
                }

                observations.Add(new Observation
                {
                    MAC = ExtractValue(description, "Mac"),
                    SSID = ssid,
                    AuthMode = authMode,
                    FirstSeen = DateTime.Parse(ExtractValue(description, "Last Active")),
                    Channel = int.TryParse(ExtractValue(description, "Channel"), out var channel) ? channel : (int?)null,
                    RSSI = rssi,
                    CurrentLatitude = coordinates[1],
                    CurrentLongitude = coordinates[0],
                    AltitudeMeters = coordinates[2],
                    MfgrId = ExtractValue(description, "Manufacturer"),
                    Type = "WIFI"
                });
            }

            if (unknownSignalCount > 0)
                logger.LogWarning("Imported {count} observations with unknown RSSI; using {rssi} ({fileName})",
                    unknownSignalCount, UnknownRssi, fileName);

            return observations;
        }

        /// <summary>
        /// Read a description field up to its line break or the next label. An empty value must
        /// not pick up text from a later field, and inline value markup is allowed.
        /// </summary>
        private static string ExtractValue(HtmlDocument htmlDoc, string label)
        {
            var labelNode = htmlDoc.DocumentNode.SelectNodes("//b")?.FirstOrDefault(node =>
                HtmlEntity.DeEntitize(node.InnerText).Trim().Equals(label + ":", StringComparison.OrdinalIgnoreCase));
            if (labelNode == null) return string.Empty;

            var value = new StringBuilder();
            for (var node = labelNode.NextSibling; node != null; node = node.NextSibling)
            {
                if (node.Name == "br" || node.Name == "b" && HtmlEntity.DeEntitize(node.InnerText).TrimEnd().EndsWith(':'))
                    break;
                if (node.NodeType != HtmlNodeType.Comment) value.Append(node.InnerText);
            }
            return HtmlEntity.DeEntitize(value.ToString()).Trim();
        }
    }
}
