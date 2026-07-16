using System;
using System.Globalization;

namespace OCIMS
{
    /// <summary>
    /// Culture-safe formatting/parsing for the display strings the app stores in
    /// its models ("MMM dd, yyyy" dates, comma-grouped amounts). Always uses
    /// InvariantCulture so saving works on any Windows locale.
    /// </summary>
    public static class AppFormats
    {
        public const string DateFormat = "MMM dd, yyyy";

        public static string ToDisplayDate(DateTime date)
        {
            return date.ToString(DateFormat, CultureInfo.InvariantCulture);
        }

        public static bool TryParseDisplayDate(string text, out DateTime date)
        {
            date = default(DateTime);
            if (string.IsNullOrWhiteSpace(text) || text == "—") return false;

            if (DateTime.TryParseExact(text.Trim(), DateFormat, CultureInfo.InvariantCulture,
                    DateTimeStyles.None, out date))
                return true;

            // Older rows may hold other shapes (e.g. "2026-07-16"); try leniently.
            return DateTime.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.None, out date)
                || DateTime.TryParse(text, CultureInfo.CurrentCulture, DateTimeStyles.None, out date);
        }

        /// <summary>Parses a user-typed money amount ("12,500.50", "₱1,000") and requires it to be positive.</summary>
        public static bool TryParseAmount(string text, out decimal amount)
        {
            amount = 0m;
            if (string.IsNullOrWhiteSpace(text)) return false;
            text = text.Replace("₱", "").Trim();
            return decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out amount)
                && amount > 0m;
        }
    }
}
