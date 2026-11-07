using CsvHelper;
using CsvHelper.Configuration;
using Microsoft.Extensions.Logging;
using System.Globalization;
using Wistellar.Core.Models;
using Wistellar.Core.Services.MobileNetwork;

namespace Wistellar.Core.Import
{
    /// <summary>
    /// Imports the CSV logs written by the Network Survey Android app (craxiom-wifi-*.csv,
    /// craxiom-bluetooth-*.csv, craxiom-lte-*.csv, ...). Each file holds one technology and starts
    /// with a block of '#' comment lines; the technology is recognised from the column header that
    /// follows them. Fields are read by name, because later CSV versions append columns.
    /// </summary>
    public class CsvNetworkSurveyImport(
        ILogger<CsvNetworkSurveyImport> logger,
        MobileNetworkResolverService mobileNetworkResolver) : ITextImport
    {
        private const string Banner = "# Created by Network Survey";
        private const string ColumnHeader = "deviceTime,latitude,longitude,";
        // Match the other importers' marker for unavailable signal strength.
        private const int UnknownRssi = -200;

        static readonly CsvConfiguration csvConfig = new(CultureInfo.InvariantCulture)
        {
            HasHeaderRecord = false
        };

        public string Name => nameof(CsvNetworkSurveyImport);

        public bool Detect(string contentType, string header) =>
            header.StartsWith(Banner, StringComparison.Ordinal)
            || header.StartsWith(ColumnHeader, StringComparison.Ordinal);

        public async Task<IEnumerable<Observation>> Import(string header, TextReader reader, CancellationToken ct = default)
        {
            var columnLine = header;
            while (columnLine != null && columnLine.StartsWith('#'))
                columnLine = await reader.ReadLineAsync(ct);
            if (columnLine == null)
                return [];

            // Column names are plain identifiers, so the header row needs no CSV unquoting.
            var columns = new Dictionary<string, int>();
            foreach (var (name, index) in columnLine.Split(',').Select((name, index) => (name.Trim(), index)))
                columns.TryAdd(name, index);

            var map = MapperFor(columns);
            if (map == null)
            {
                // GNSS, device status, phone state and call detail logs describe the phone, not networks.
                logger.LogWarning("Network Survey file has no network records to import (columns: {columns})", columnLine);
                return [];
            }

            var observations = new List<Observation>();
            var skipped = 0;
            using var parser = new CsvParser(reader, csvConfig, leaveOpen: true);
            while (await parser.ReadAsync())
            {
                ct.ThrowIfCancellationRequested();
                var observation = map(new Row(columns, parser.Record ?? []));
                if (observation == null)
                    skipped++;
                else
                    observations.Add(observation);
            }

            // Neighbour cells are often reported by channel and PCI alone, and records logged before
            // the first GPS fix have no position; neither can be placed on the map as a network.
            if (skipped > 0)
                logger.LogInformation("Skipped {count} Network Survey records without a position or network identity", skipped);

            return observations;
        }

        private Func<Row, Observation?>? MapperFor(Dictionary<string, int> columns)
        {
            // Wi-Fi files also have a sourceAddress column, so bssid has to be checked first.
            if (columns.ContainsKey("bssid"))
                return Wifi;
            if (columns.ContainsKey("sourceAddress"))
                return Bluetooth;
            // LTE's signalStrength is the RSSI; RSRP is what the WiGLE app records as the level.
            if (columns.ContainsKey("eci"))
                return row => Cell(row, "LTE", "tac", "eci", "earfcn", row.Number("rsrp") ?? row.Number("signalStrength"));
            if (columns.ContainsKey("nci"))
                return row => Cell(row, "NR", "tac", "nci", "narfcn", row.Number("ssRsrp") ?? row.Number("csiRsrp"));
            if (columns.ContainsKey("cid"))
                return row => Cell(row, "WCDMA", "lac", "cid", "uarfcn", row.Number("rscp") ?? row.Number("signalStrength"));
            if (columns.ContainsKey("ci"))
                return row => Cell(row, "GSM", "lac", "ci", "arfcn", row.Number("signalStrength"));
            if (columns.ContainsKey("bsid"))
                return Cdma;
            return null;
        }

        private static Observation? Wifi(Row row)
        {
            var observation = Observe(row, "WIFI", row.Text("bssid"), row.Number("signalStrength"));
            if (observation == null)
                return null;

            observation.SSID = row.Text("ssid");
            observation.AuthMode = WifiCapabilities(row);
            observation.Channel = row.Integer("channel");
            observation.Frequency = row.Integer("frequencyMhz");
            return observation;
        }

        /// <summary>
        /// Network Survey reduces Android's capability string to a single encryption type such as
        /// WPA_WPA2. Spell it back out in WiGLE's bracket form so that capability filters such as
        /// cap=%WPA2% match networks from either source.
        /// </summary>
        private static string WifiCapabilities(Row row)
        {
            var encryption = row.Text("encryptionType");
            IEnumerable<string> capabilities = encryption is "" or "OPEN" or "UNKNOWN"
                ? []
                : encryption.Split('_').Select(part => $"[{part}]");
            if (row.Text("wps") == "true")
                capabilities = capabilities.Append("[WPS]");
            return string.Concat(capabilities);
        }

        private static Observation? Bluetooth(Row row)
        {
            var type = IsLowEnergy(row) ? "BLE" : "BT";
            var observation = Observe(row, type, row.Text("sourceAddress"), row.Number("signalStrength"));
            if (observation == null)
                return null;

            observation.SSID = row.Text("otaDeviceName");
            observation.AuthMode = string.Join(";", new[] { row.Text("supportedTechnologies"), row.Text("addressType") }
                .Where(value => value is not ("" or "UNKNOWN")));
            observation.Channel = row.Integer("channel");
            // Network Survey writes the company ID in hex; WiGLE's MfgrId column is decimal.
            if (int.TryParse(row.Text("companyId"), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var companyId))
                observation.MfgrId = companyId.ToString(CultureInfo.InvariantCulture);
            return observation;
        }

        /// <summary>
        /// Recent versions mark every BLE scan result technology=LE and never claim BR_EDR; older
        /// ones left the field UNKNOWN. Service UUIDs, the company ID and manufacturer data are only
        /// ever taken from an LE advertisement, so they identify BLE records in older files too.
        /// </summary>
        private static bool IsLowEnergy(Row row) => row.Text("technology") switch
        {
            "LE" => true,
            "BR_EDR" => false,
            _ => row.Text("supportedTechnologies") == "LE"
                || row.Text("serviceUuids") != ""
                || row.Text("companyId") != ""
                || row.Text("mfgData") != "",
        };

        private Observation? Cell(Row row, string type, string areaColumn, string cellColumn, string channelColumn, double? signal)
        {
            var plmn = Plmn(row);
            var area = row.Text(areaColumn);
            var cell = row.Text(cellColumn);
            if (plmn == null || area == "" || cell == "")
                return null;

            // Cell sites have no MAC; use the same operator_area_cell key as the other cell importers.
            var observation = Observe(row, type, $"{plmn}_{area}_{cell}", signal);
            if (observation == null)
                return null;

            observation.SSID = mobileNetworkResolver.Get(plmn) ?? row.Text("provider");
            observation.AuthMode = $"{type};{plmn}";
            observation.Channel = row.Integer(channelColumn);
            return observation;
        }

        private static string? Plmn(Row row)
        {
            // Versions with a plmn column write it as "262-02", keeping the MNC's own digits (a
            // three-digit MNC can start with 0). Older files only have the numeric mcc and mnc.
            var plmn = row.Text("plmn").Replace("-", "");
            if (plmn != "")
                return plmn;
            return row.Integer("mcc") is { } mcc && row.Integer("mnc") is { } mnc
                ? string.Format(CultureInfo.InvariantCulture, "{0}{1:D2}", mcc, mnc)
                : null;
        }

        private static Observation? Cdma(Row row)
        {
            var sid = row.Text("sid");
            var nid = row.Text("nid");
            var bsid = row.Text("bsid");
            if (sid == "" || nid == "" || bsid == "")
                return null;

            var observation = Observe(row, "CDMA", $"{sid}_{nid}_{bsid}", row.Number("signalStrength"));
            if (observation == null)
                return null;

            observation.SSID = row.Text("provider");
            observation.AuthMode = "CDMA";
            observation.Channel = row.Integer("channel");
            return observation;
        }

        /// <summary>
        /// Builds the part of an observation that every technology shares, or returns null for a
        /// record that cannot be placed on the map.
        /// </summary>
        private static Observation? Observe(Row row, string type, string mac, double? signal)
        {
            if (mac == ""
                || !DateTimeOffset.TryParse(row.Text("deviceTime"), CultureInfo.InvariantCulture, DateTimeStyles.None, out var time)
                || row.Number("latitude") is not { } latitude
                || row.Number("longitude") is not { } longitude
                // Records logged before the first GPS fix are written at 0,0.
                || latitude == 0 && longitude == 0)
                return null;

            return new Observation
            {
                Type = type,
                MAC = mac,
                FirstSeen = time.UtcDateTime,
                CurrentLatitude = latitude,
                CurrentLongitude = longitude,
                AltitudeMeters = row.Number("altitude") ?? 0,
                AccuracyMeters = row.Number("accuracy") ?? 0,
                RSSI = signal is { } dbm ? (int)Math.Round(dbm) : UnknownRssi,
            };
        }

        /// <summary>One CSV record, read by column name.</summary>
        private sealed class Row(Dictionary<string, int> columns, string[] fields)
        {
            public string Text(string column) =>
                columns.TryGetValue(column, out var index) && index < fields.Length ? fields[index].Trim() : "";

            // Location columns use the recording phone's decimal separator ("51,373713" on a German
            // phone, quoted because of the comma) and are never grouped, so a comma is a decimal point.
            public double? Number(string column) =>
                double.TryParse(Text(column).Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out var value)
                && double.IsFinite(value) ? value : null;

            public int? Integer(string column) =>
                int.TryParse(Text(column), NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) ? value : null;
        }
    }
}
