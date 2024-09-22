using DokkanDaily.Models;
using System.Text.Json;

namespace DokkanDaily.Helpers
{
    public static class DDHelper
    {
        private static readonly JsonSerializerOptions options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

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
    }
}
