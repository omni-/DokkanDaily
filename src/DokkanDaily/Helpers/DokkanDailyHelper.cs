using DokkanDaily.Constants;
using DokkanDaily.Models;
using DokkanDaily.Models.Enums;
using DokkanDaily.Models.Net;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace DokkanDaily.Helpers
{
    public static partial class DokkanDailyHelper
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

        public static Unit GetUnit(string name, string title)
        {
            return DokkanConstants.UnitDB.FirstOrDefault(x => x.Name == name && x.Title == title);
        }

        public static Unit GetUnit(Leader leader)
        {
            return DokkanConstants.UnitDB.FirstOrDefault(x => x.Name == leader.Name && x.Title == leader.Title);
        }

        public static string GetChallengeText(this Challenge challenge, bool useDiscordFormatting = false)
        {
            string star = useDiscordFormatting ? "*" : "";
            string text = challenge.DailyType switch
            {
                DailyType.Character => $"{star}{challenge.Leader.FullName}{star} as the leader",
                DailyType.Category => $"only units belonging to the {star}{challenge.Category.Name}{star} category",
                DailyType.LinkSkill => $"only units with the link skill {star}{challenge.LinkSkill.Name}{star}",
                _ => throw new ArgumentException("Reached unreachable code. Yay!")
            };
            return $"Defeat {star}{star}{challenge.TodaysEvent.FullName}{star}{star} using {text}";
        }

        public static WebhookPayload ToWebhookPayload(this Challenge challenge)
        {
            return new() { Content = $"# Daily Challenge!\r\n{challenge.GetChallengeText(true)}!\r\n\r\n<@&1289820573949497345>\r\n\r\nSite Link: https://dokkandle.net/daily" };
        }
    }
}
