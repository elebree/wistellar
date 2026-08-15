using HtmlAgilityPack;
using Microsoft.Extensions.Logging;
using System.Globalization;
using System.Xml.Linq;
using Wistellar.Core.Models;

namespace Wistellar.Core.Import
{
    public class WifiDbImport(ILogger logger)
    {
        private static Dictionary<string, string> AuthenticationMap = new Dictionary<string, string>()
        {
            { "Otevýen\u0082" ,"ESS"},
            { "Otwarte" ,"ESS"},
            { "Ouvrir" ,"ESS"},
            { "Abierta" ,"ESS"},
            { "Aperta" ,"ESS"},
            { "Offen" ,"ESS"},
            { "Open" ,"ESS"},
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
            { "wpa2-personal" ,"WPA2-PSK"},
            { "WPA2" ,"WPA2-PSK"},
            { "WPA2-PSK" ,"WPA2-PSK"},
            { "WPA2-Personal" ,"WPA2-PSK"},
            { "WPA2-osobn¡" ,"WPA2-PSK"},
            { "WPA2ÿ-ÿPersonnel" ,"WPA2-PSK"},
            { "WPA2ÿ-ÿEntreprise" ,"WPA2-EAP"},
            { "WPA2-Enterprise" ,"WPA2-EAP"},
            { "WPA3-Personal" ,"WPA3-PSK"},
        };

        private static Dictionary<string, string> EncryptionMap = new Dictionary<string, string>()
        {
            { "TKIP","TKIP" },
            { "WEP","WEP" },
            { "CCMP","CCMP" },
            { "None","NONE" },
            { "Nessuno","NONE" },
            { "Ninguna","NONE" },
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

                observations.Add(new Observation
                {
                    MAC = ExtractValue(description, "Mac"),
                    SSID = ssid,
                    AuthMode = authMode,
                    FirstSeen = DateTime.Parse(ExtractValue(description, "Last Active")),
                    Channel = int.TryParse(ExtractValue(description, "Channel"), out var channel) ? channel : (int?)null,
                    RSSI = int.Parse(ExtractValue(description, "High GPS RSSI")),
                    CurrentLatitude = coordinates[1],
                    CurrentLongitude = coordinates[0],
                    AltitudeMeters = coordinates[2],
                    MfgrId = ExtractValue(description, "Manufacturer"),
                    Type = "WIFI"
                });
            }

            return observations;
        }

        /// <summary>
        /// wifidb KML carries each field as a bold label followed by a bare text node, so values are
        /// read out of the description HTML rather than from KML elements.
        /// </summary>
        private static string ExtractValue(HtmlDocument htmlDoc, string label)
        {
            label += ": ";
            var node = htmlDoc.DocumentNode.SelectSingleNode($"//b[text()='{label}']/following-sibling::text()[1]");
            return node?.InnerText.Trim() ?? string.Empty;
        }
    }
}
