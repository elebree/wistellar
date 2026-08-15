using CsvHelper;
using CsvHelper.Configuration;
using CsvHelper.Configuration.Attributes;
using System.Globalization;
using Wistellar.Core.Import;
using Wistellar.Core.Models;
using Wistellar.Core.Services.MobileNetwork;

namespace Wistellar.Server.Import
{
    public class CsvOpenCellIdImport(
        MobileNetworkResolverService mobileNetworkResolver) : ITextImport
    {
        static readonly string[] radio = ["GSM", "UMTS", "LTE", "CDMA", "NR"];
        static readonly string[] headers = ["radio", "mcc", "net", "area", "cell", "unit", "lon", "lat", "range", "samples", "changeable", "created", "updated", "averageSignal"];
        static readonly CsvConfiguration csvConfig = new(CultureInfo.InvariantCulture)
        {
            HasHeaderRecord = false
        };

        public string Name => nameof(CsvOpenCellIdImport);

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
            var columns = header.Split(",").ToArray();
            var csv = new CsvReader(new StringReader(header), csvConfig);
            var containsHeader = (columns.Length == headers.Length && headers.Zip(columns).All(x => x.First.Equals(x.Second, StringComparison.CurrentCultureIgnoreCase)));
            var firstTower = containsHeader ? [] : csv.GetRecords<CellTower>().ToArray();
            csv = new CsvReader(reader, csvConfig);
            var towers = csv.GetRecords<CellTower>();

            return Task.FromResult(firstTower.Concat(towers).Select(Map));
        }

        private Observation Map(CellTower tower)
        {
            var plmn = string.Format("{0}{1:D2}", tower.Mcc, tower.Net);
            var type = tower.Radio == "UMTS" ? "WCDMA" : tower.Radio;
            string[] cap = [type, plmn];
            var o = new Observation()
            {
                Type = tower.Radio == "UMTS" ? "WCDMA" : tower.Radio,
                MAC = string.Format("{0}_{1}_{2}", plmn, tower.Area, tower.Cell),
                SSID = mobileNetworkResolver.Get(plmn) ?? "",
                //CAP
                AuthMode = string.Join(";", cap.Where(v => v != null)),
                FirstSeen = DateTimeOffset.FromUnixTimeSeconds(tower.Updated).UtcDateTime,
                CurrentLatitude = tower.Lat,
                CurrentLongitude = tower.Lon,
            };

            return o;
        }
    }


    /// <summary>
    /// Class representing columns of the csv files from the opencellid.org database.
    /// </summary>
    public class CellTower
    {
        /// <summary>
        /// Network type. One of the strings GSM, UMTS, LTE, or CDMA.
        /// </summary>
        [Index(0)]
        public string Radio { get; set; } = "";

        [Index(1)]
        /// <summary>
        /// Mobile Country Code, for example, 260 for Poland.
        /// </summary>
        public uint Mcc { get; set; }

        [Index(2)]
        /// <summary>
        /// For GSM, UMTS, and LTE networks, this is the Mobile Network Code (MNC).
        /// For CDMA networks, this is the System IDentification number (SID).
        /// </summary>
        public uint Net { get; set; }

        [Index(3)]
        /// <summary>
        /// Location Area Code (LAC) for GSM and UMTS networks.
        /// Tracking Area Code (TAC) for LTE networks.
        /// Network IDenfitication number (NID) for CDMA networks.
        /// </summary>
        public uint Area { get; set; }

        [Index(4)]
        /// <summary>
        /// Cell ID (CID) for GSM and LTE networks.
        /// UTRAN Cell ID / LCID for UMTS networks.
        /// Base station IDentifier number (BID) for CDMA networks.
        /// </summary>
        public ulong Cell { get; set; }

        [Index(5)]
        /// <summary>
        /// Primary Scrambling Code (PSC) for UMTS networks.
        /// Physical Cell ID (PCI) for LTE networks. Empty for GSM and CDMA networks.
        /// </summary>
        public int Unit { get; set; }

        [Index(6)]
        /// <summary>
        /// Longitude in degrees between -180.0 and 180.0.
        /// changeable=1: average of longitude values of all related measurements.
        /// changeable=0: exact GPS position of the cell tower.
        /// </summary>
        public double Lon { get; set; }

        [Index(7)]
        /// <summary>
        /// Latitude in degrees between -90.0 and 90.0.
        /// changeable=1: average of latitude values of all related measurements.
        /// changeable=0: exact GPS position of the tower.
        /// </summary>
        public double Lat { get; set; }

        [Index(8)]
        /// <summary>
        /// Estimate of cell range, in meters.
        /// </summary>
        public uint Range { get; set; }

        [Index(9)]
        /// <summary>
        /// Total number of measurements assigned to the cell tower.
        /// </summary>
        public uint Samples { get; set; }

        [Index(10)]
        /// <summary>
        /// Defines if coordinates of the cell tower are exact or approximate.
        /// changeable=1: calculated from all available measurements.
        /// changeable=0: precise GPS position without calculations.
        /// </summary>
        public int Changeable { get; set; }

        [Index(11)]
        /// <summary>
        /// The first time the cell tower was seen and added to the database.
        /// A date in timestamp format (seconds since Unix Epoch 1970-01-01T00:00:00Z).
        /// </summary>
        public uint Created { get; set; }

        [Index(12)]
        /// <summary>
        /// The last time the cell tower was seen and updated.
        /// A date in timestamp format (seconds since Unix Epoch 1970-01-01T00:00:00Z).
        /// </summary>
        public uint Updated { get; set; }

        [Index(13)]
        /// <summary>
        /// Average signal strength from all assigned measurements for the cell.
        /// Either in dBm or as defined in TS 27.007 8.5.
        /// </summary>
        public int AverageSignal { get; set; }
    }
}
