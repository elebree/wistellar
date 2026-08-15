using System.Text.RegularExpressions;

namespace Wistellar.Core.Extensions
{
    public static class DateExtensions
    {
        // Convert datetime to UNIX time
        public static long ToUnixTimeSeconds(this DateTime dateTime)
        {
            return new DateTimeOffset(dateTime.ToUniversalTime()).ToUnixTimeSeconds();
        }

        private static bool TryParseISO8601(string text, out DateTime unixTime)
        {
            unixTime = DateTime.TryParse(text, null, System.Globalization.DateTimeStyles.AssumeUniversal, out var date)
                ? date.ToUniversalTime()
                : DateTime.MinValue;
            return unixTime != DateTime.MinValue;
        }

        public static bool TryParseDuration(string? input, out TimeSpan value)
        {
            if (input == null)
            {
                value = TimeSpan.Zero;
                return false;
            }
            else if (TryParseDurationWithUnits(input, out value))
            {
                return true;
            }
            else if (long.TryParse(input, out var seconds))
            {
                value = TimeSpan.FromSeconds(seconds);
                return true;
            }
            else
            {
                value = TimeSpan.Zero;
                return false;
            }
        }

        public static bool TryParseDate(string? input, out DateTime value)
        {
            var date = DateTime.UtcNow;

            if (input == null)
            {
                value = DateTime.MinValue;
                return false;
            }
            else if (TryParseDurationWithUnits(input, out var span, date)) // relative date
            {
                value = date.Subtract(span);
            }
            else if (TryParseISO8601(input, out date)) // date in ISO format
            {
                value = date;
            }
            else if (long.TryParse(input, out long unixTimeInSeconds)) // unix time seconds
            {
                value = DateTime.UnixEpoch.AddSeconds(unixTimeInSeconds);

            }
            else
            {
                value = DateTime.MinValue;
                return false;
            }

            return true;
        }

        private static bool TryParseDurationWithUnits(string input, out TimeSpan result, DateTime? referenceDate = null)
        {
            result = TimeSpan.Zero;
            var now = referenceDate ?? DateTime.UtcNow;
            var regex = new Regex(@"(?<value>\d+)(?<unit>[yMdhm])");
            var tempDate = now;
            bool matched = false;

            foreach (Match match in regex.Matches(input))
            {
                if (!int.TryParse(match.Groups["value"].Value, out int value))
                    return false;

                string unit = match.Groups["unit"].Value;
                matched = true;

                try
                {
                    tempDate = unit switch
                    {
                        "y" => tempDate.AddYears(-value),
                        "M" => tempDate.AddMonths(-value),
                        "d" => tempDate.AddDays(-value),
                        "h" => tempDate.AddHours(-value),
                        "m" => tempDate.AddMinutes(-value),
                        _ => tempDate
                    };
                }
                catch
                {
                    return false; // Handle overflow cases
                }
            }

            if (!matched) return false; // No valid matches found

            result = now - tempDate;
            return true;
        }
    }
}