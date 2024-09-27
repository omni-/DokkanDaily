using DokkanDaily.Constants;
using DokkanDaily.Models;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace DokkanDaily.Helpers
{
    public static partial class DDHelper
    {
        private static readonly JsonSerializerOptions options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };


        [GeneratedRegex("[^a-zA-Z0-9-]")]
        public static partial Regex AlphaNumericRegex();

        public static IEnumerable<Unit> BuildCharacterDb()
        {
            Stream s = File.OpenRead("./wwwroot/data/DokkanCharacterData.json");

            var result = JsonSerializer.Deserialize<IEnumerable<Unit>>(s, options);

            foreach (var unit in result)
                if (unit.ImageURL.Contains("static."))
                    unit.ImageURL = unit.ImageURL.Replace("static.", "vignette.");

            return result;
        }

        public static string GetUtcNowDateTag()
        {
            return DateTime.UtcNow.ToString("MM-dd-yyyy");
        }

        public static string GetTagFromDate(this DateTime date)
        {
            return date.ToString("MM-dd-yyyy");
        }

        public static string GetTagValueOrDefault(this IDictionary<string, string> dictionary, string tagName)
        {
            if (dictionary == null || !dictionary.TryGetValue(tagName, out string result)) return null;

            return result;
        }

        public static bool TryParseDokkanTimeSpan(string value, out TimeSpan result)
        {
            return TimeSpan.TryParseExact(value, "h\\'mm\\\"ss\\.f", System.Globalization.CultureInfo.InvariantCulture, out result);
        }


        public static string AddUserAgentToFileName(string file, string userAgent)
        {
            if (string.IsNullOrEmpty(userAgent)) return file;

            string ext = Path.GetExtension(file);
            string name = Path.GetFileNameWithoutExtension(file);
            string agentPart = string.IsNullOrEmpty(userAgent) ? "" : $"-{AlphaNumericRegex().Replace(userAgent, "")}";

            return $"{name}{agentPart}{ext}";
        }
    }
}
