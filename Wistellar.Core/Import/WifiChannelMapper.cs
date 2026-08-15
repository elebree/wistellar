namespace Wistellar.Core.Import
{
    using System.Collections.Generic;

    class WiFiChannelMapper
    {
        private static readonly Dictionary<string, Dictionary<int, int>> wifiFrequencies = new()
        {
            ["2.4GHz"] = new Dictionary<int, int>
        {
            { 1, 2412 }, { 2, 2417 }, { 3, 2422 }, { 4, 2427 },
            { 5, 2432 }, { 6, 2437 }, { 7, 2442 }, { 8, 2447 },
            { 9, 2452 }, { 10, 2457 }, { 11, 2462 }, { 12, 2467 },
            { 13, 2472 }, { 14, 2484 }
        },
            ["5GHz"] = new Dictionary<int, int>
        {
            { 36, 5180 }, { 40, 5200 }, { 44, 5220 }, { 48, 5240 },
            { 52, 5260 }, { 56, 5280 }, { 60, 5300 }, { 64, 5320 },
            { 100, 5500 }, { 104, 5520 }, { 108, 5540 }, { 112, 5560 },
            { 116, 5580 }, { 120, 5600 }, { 124, 5620 }, { 128, 5640 },
            { 132, 5660 }, { 136, 5680 }, { 140, 5700 }, { 144, 5720 },
            { 149, 5745 }, { 153, 5765 }, { 157, 5785 }, { 161, 5805 },
            { 165, 5825 }
        },
            ["6GHz"] = new Dictionary<int, int>
        {
            { 1, 5955 }, { 5, 5975 }, { 9, 5995 }, { 13, 6015 },
            { 17, 6035 }, { 21, 6055 }, { 25, 6075 }, { 29, 6095 },
            { 33, 6115 }, { 37, 6135 }, { 41, 6155 }, { 45, 6175 },
            { 49, 6195 }, { 53, 6215 }, { 57, 6235 }, { 61, 6255 },
            { 65, 6275 }, { 69, 6295 }, { 73, 6315 }, { 77, 6335 },
            { 81, 6355 }, { 85, 6375 }, { 89, 6395 }, { 93, 6415 },
            { 97, 6435 }, { 101, 6455 }, { 105, 6475 }, { 109, 6495 },
            { 113, 6515 }, { 117, 6535 }, { 121, 6555 }, { 125, 6575 },
            { 129, 6595 }, { 133, 6615 }, { 137, 6635 }, { 141, 6655 },
            { 145, 6675 }, { 149, 6695 }, { 153, 6715 }, { 157, 6735 },
            { 161, 6755 }, { 165, 6775 }, { 169, 6795 }, { 173, 6815 },
            { 177, 6835 }, { 181, 6855 }, { 185, 6875 }, { 189, 6895 },
            { 193, 6915 }, { 197, 6935 }, { 201, 6955 }, { 205, 6975 },
            { 209, 6995 }, { 213, 7015 }, { 217, 7035 }, { 221, 7055 },
            { 225, 7075 }, { 229, 7095 }, { 233, 7115 }
        }
        };

        public static int? GetCenterFrequencyForBand(string band, int channel)
        {
            switch (band)
            {
                case "2.4GHz":
                    if (channel >= 1 && channel <= 14) // Valid 2.4GHz channels
                        return 2402 + 5 * (channel - 1); // Channel 1 -> 2402 MHz, etc.
                    break;

                case "5GHz":
                    if (channel >= 36 && channel <= 165) // Valid 5GHz channels
                        return 5170 + 5 * (channel - 36); // Channel 36 -> 5170 MHz, etc.
                    break;

                case "6GHz":
                    if (channel >= 1 && channel <= 233) // Valid 6GHz channels
                        return 5945 + 5 * (channel - 1); // Channel 1 -> 5945 MHz, etc.
                    break;

                default:
                    return null; // Invalid band
            }

            return null; // Invalid channel for given band
        }

        public static int? GetCenterFrequency(string standard, int channel)
        {
            // Determine valid bands for the given standard
            List<string>? bands = standard switch
            {
                "802.11b" or "802.11g" => new() { "2.4GHz" },
                "802.11n" => ["2.4GHz", "5GHz"],
                "802.11a" => ["5GHz"],
                "802.11ac" => ["5GHz"],
                "802.11ax" => ["2.4GHz", "5GHz", "6GHz"],
                _ => null
            };

            if (bands == null)
                return null;

            // Search only allowed bands
            foreach (var band in bands)
            {
                var freq = GetCenterFrequencyForBand(band, channel);
                if (freq != null)
                    return freq;
            }

            return null; // Channel not valid for that standard
        }
    }
}
