using DokkanDaily.Constants;
using DokkanDaily.Models;
using DokkanDaily.Models.Enums;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace DokkanDaily.Helpers
{
    public static partial class DokkanDailyHelper
    {
        private static readonly JsonSerializerOptions options = new() { PropertyNameCaseInsensitive = true };

        public static readonly Dictionary<string, string> KnownUsernameMap = new()
        {
            // pattern, value
            { "五.悟", "五条悟" },
            { "UBCeomnt", "DBC*omni" }
        };

        [GeneratedRegex("[^a-zA-Z0-9-]")]
        public static partial Regex AlphaNumericRegex();

        [GeneratedRegex(@"([UDO]BC\s?[\*\+]\s?).*")]
        public static partial Regex DbcNicknameTagRegex();

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
            => DateTime.UtcNow.ToString("MM-dd-yyyy");

        public static string GetTagFromDate(this DateTime date)
            => date.ToString("MM-dd-yyyy");

        public static string GetTagValueOrDefault(this IDictionary<string, string> dictionary, string tagName)
        {
            if (dictionary == null || !dictionary.TryGetValue(tagName, out string result)) return null;

            return result;
        }

        public static bool TryParseDokkanTimeSpan(string value, out TimeSpan result)
            => TimeSpan.TryParseExact(value, "h\\'mm\\\"ss\\.f", System.Globalization.CultureInfo.InvariantCulture, out result);

        public static string AddUserAgentToFileName(string file, string userAgent)
        {
            if (string.IsNullOrEmpty(userAgent)) return file;

            string ext = Path.GetExtension(file);
            string name = Path.GetFileNameWithoutExtension(file);
            string agentPart = string.IsNullOrEmpty(userAgent) ? "" : $"-{AlphaNumericRegex().Replace(userAgent, "")}";

            return $"{name}{agentPart}{ext}";
        }

        public static Unit GetUnit(string name, string title)
            => DokkanConstants.UnitDB.FirstOrDefault(x => x.Name == name && x.Title == title);

        public static Unit GetUnit(Leader leader)
            => DokkanConstants.UnitDB.FirstOrDefault(x => x.Name == leader.Name && x.Title == leader.Title);

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

        public static WebhookMessage ToWebhookPayload(this Challenge challenge) => new()
        {
            Message = $"# Daily Challenge!\r\n{challenge.GetChallengeText(true)}!\r\n\r\n{InternalConstants.DokkandleDbcRole}\r\n\r\n*via https://dokkandle.net/daily*",
            FilePath = challenge.TodaysEvent?.WallpaperImagePath
        };

        public static string AddSasTokenToUri(this string uri, string sasToken)
            => $"{uri}?{sasToken}";

        public static string GetDisplayName(this LeaderboardUser leaderboardUser) =>  string.IsNullOrWhiteSpace(leaderboardUser.DiscordUsername) ? leaderboardUser.DokkanNickname : $"{leaderboardUser.DiscordUsername} ({leaderboardUser.DokkanNickname})";

        public static string AddDokkandleDbcRolePing(this string source) => $"{source}\r\n{InternalConstants.DokkandleDbcRole}";

        public static string UnescapeUnicode(string value) => string.IsNullOrWhiteSpace(value) ? value : Regex.Unescape(value);

        public static string EscapeUnicode(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return value;

            StringBuilder sb = new();
            foreach (char c in value)
            {
                if (c > 127)
                {
                    // This character is too big for ASCII
                    string encodedValue = "\\u" + ((int)c).ToString("x4");
                    sb.Append(encodedValue);
                }
                else
                {
                    sb.Append(c);
                }
            }
            return sb.ToString();
        }

        public static string CheckUsername(string username)
        {
            if (string.IsNullOrEmpty(username)) return username;

            var sub = KnownUsernameMap.Keys.FirstOrDefault(x => Regex.IsMatch(username, x));

            if (!string.IsNullOrEmpty(sub))
            {
                return KnownUsernameMap[sub];
            }
            else
            {
                Match m = DbcNicknameTagRegex().Match(username);

                if (m == null || m.Groups.Count < 2) return username;

                username = username.Replace(m.Groups[1].Value, OcrConstants.DbcTag);
            }

            return username;
        }
    }
}
