using CsvHelper;
using CsvHelper.Configuration;
using System.Globalization;
using System.Reflection;
using Wistellar.Core.Models;

namespace Wistellar.Core.Import
{
    public class CsvWigleObservationsImport : ITextImport
    {
        private readonly CsvConfiguration csvConfig = new(CultureInfo.InvariantCulture) { };

        public string Name => nameof(CsvWigleObservationsImport);

        public bool Detect(string contentType, string header)
        {
            var props = ObservationsHeader.Parse(header);
            return
                !string.IsNullOrWhiteSpace(props.Version)
                && !string.IsNullOrWhiteSpace(props.AppRelease);
        }

        public Task<IEnumerable<Observation>> Import(string header, TextReader reader)
        {
            var csv = new CsvReader(reader, csvConfig);
            csv.Context.RegisterClassMap<ObservationMap>(); // Register the mapping
            var observations = csv.GetRecords<Observation>();
            return Task.FromResult(observations);
        }
    }

    public class ObservationsHeader
    {
        public string Version { get; private set; } = "";
        public string AppRelease { get; private set; } = "";
        public string Model { get; private set; } = "";
        public string Release { get; private set; } = "";
        public string Device { get; private set; } = "";
        public string Display { get; private set; } = "";
        public string Board { get; private set; } = "";
        public string Brand { get; private set; } = "";

        public static ObservationsHeader Parse(string input)
        {
            var parts = input.Split(',');
            var info = new ObservationsHeader { Version = parts[0] };
            var properties = typeof(ObservationsHeader).GetProperties(BindingFlags.Public | BindingFlags.Instance);

            parts.Skip(1)
               .Select(part => part.Split('='))
               .ToList()
               .ForEach(kv => properties
                                .FirstOrDefault(p => string.Equals(p.Name, kv[0], StringComparison.OrdinalIgnoreCase))?
                                .SetValue(info, kv[1]));
            return info;
        }
    }

    public class ObservationMap : ClassMap<Observation>
    {
        public ObservationMap()
        {
            Map(m => m.MAC).Name("MAC");
            Map(m => m.SSID).Name("SSID");
            Map(m => m.AuthMode).Name("AuthMode");
            Map(m => m.FirstSeen).Name("FirstSeen").TypeConverterOption.Format("yyyy-MM-dd HH:mm:ss");
            Map(m => m.Channel).Name("Channel");
            Map(m => m.Frequency).Name("Frequency");
            Map(m => m.RSSI).Name("RSSI");
            Map(m => m.CurrentLatitude).Name("CurrentLatitude");
            Map(m => m.CurrentLongitude).Name("CurrentLongitude");
            Map(m => m.AltitudeMeters).Name("AltitudeMeters");
            Map(m => m.AccuracyMeters).Name("AccuracyMeters");
            Map(m => m.Type).Name("RCOIs");
            Map(m => m.Type).Name("MfgrId");
            Map(m => m.Type).Name("Type");
        }
    }
}
