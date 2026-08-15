using CsvHelper;
using CsvHelper.Configuration;
using CsvHelper.Configuration.Attributes;
using System.Globalization;
using Wistellar.Core.Models;
using Wistellar.Core.Services.MobileNetwork;

namespace Wistellar.Core.Import
{
    public class CsvMylnikovCellImport(
        MobileNetworkResolverService mobileNetworkResolver) : ITextImport
    {
        static readonly string[] radio = ["GSM", "WCDM", "LTE", "CDMA", "UMTS", "NR"];
        static readonly string[] headers = ["id", "data_source", "radio_type", "mcc", "mnc", "lac", "cellid", "lat", "lon", "range", "created", "updated"];
        static readonly CsvConfiguration csvConfig = new(CultureInfo.InvariantCulture)
        {
            HasHeaderRecord = false
        };

        public string Name => nameof(CsvMylnikovCellImport);

        public bool Detect(string contentType, string header)
        {
            var columns = header.Split(",").ToArray();
            if (columns.Length != headers.Length)
                return false;

            if (headers.Zip(columns).All(x => x.First.Equals(x.Second, StringComparison.CurrentCultureIgnoreCase)))
                return true;

            if (radio.Contains(columns[0]))
                return true;

            return false;
        }
        public Task<IEnumerable<Observation>> Import(string header, TextReader reader)
        {
            var csv = new CsvReader(reader, csvConfig);
            var towers = csv.GetRecords<MylnikovCellTower>()
                .Select(Map)
                .ToArray();

            return Task.FromResult<IEnumerable<Observation>>(towers);
        }

        private Observation Map(MylnikovCellTower tower)
        {
            var plmn = string.Format("{0}{1:D2}", tower.Mcc, tower.Mnc);
            var type = (tower.RadioType == "WCDM" || tower.RadioType == "UMTS") ? "WCDMA" : tower.RadioType;

            return new Observation()
            {
                Type = type,
                // Cell sites have no MAC, so a synthetic identifier stands in for the BSSID key.
                MAC = string.Format("{0}{1:D2}_{2}_{3}", tower.Mcc, tower.Mnc, tower.Lac, tower.CellId),
                SSID = mobileNetworkResolver.Get(plmn) ?? "",
                AuthMode = type,
                FirstSeen = DateTimeOffset.FromUnixTimeSeconds(tower.Updated).UtcDateTime,
                CurrentLatitude = tower.Lat,
                CurrentLongitude = tower.Lon,
                // The dataset carries no signal level; -200 dBm marks "unknown" without beating a
                // real observation in the strongest-signal comparison used when merging networks.
                RSSI = -200,
            };
        }
    }

    /// <summary>
    /// Columns of the CSV export published by the Mylnikov geolocation database.
    /// </summary>
    public class MylnikovCellTower
    {
        /// <summary>Unique identifier for the record.</summary>
        [Name("id"), Index(0)]
        public long Id { get; set; }

        /// <summary>Data source identifier (-1 indicates an unknown source).</summary>
        [Name("data_source"), Index(1)]
        public int DataSource { get; set; }

        /// <summary>Type of radio network (e.g. WCDMA, LTE).</summary>
        [Name("radio_type"), Index(2)]
        public string RadioType { get; set; } = "";

        /// <summary>Mobile country code.</summary>
        [Name("mcc"), Index(3)]
        public int Mcc { get; set; }

        /// <summary>Mobile network code.</summary>
        [Name("mnc"), Index(4)]
        public int Mnc { get; set; }

        /// <summary>Location area code.</summary>
        [Name("lac"), Index(5)]
        public int Lac { get; set; }

        /// <summary>Unique cell tower identifier.</summary>
        [Name("cellid"), Index(6)]
        public long CellId { get; set; }

        /// <summary>Latitude of the cell tower.</summary>
        [Name("lat"), Index(7)]
        public double Lat { get; set; }

        /// <summary>Longitude of the cell tower.</summary>
        [Name("lon"), Index(8)]
        public double Lon { get; set; }

        /// <summary>Estimated range of the cell tower, in metres.</summary>
        [Name("range"), Index(9)]
        public int Range { get; set; }

        /// <summary>Timestamp when the record was created (Unix seconds).</summary>
        [Name("created"), Index(10)]
        public long Created { get; set; }

        /// <summary>Timestamp when the record was last updated (Unix seconds).</summary>
        [Name("updated"), Index(11)]
        public long Updated { get; set; }
    }
}