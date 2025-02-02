using DokkanDaily.Constants;
using DokkanDaily.Models;
using Microsoft.AspNetCore.Components.Authorization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace DokkanDaily.Helpers
{
    public static partial class DokkanDailyHelper
    {
        #region Regex
        [GeneratedRegex("[^a-zA-Z0-9-]")]
        public static partial Regex AlphaNumericRegex();

        [GeneratedRegex(@"([UDO]BC\s?[\*\+]\s?).*")]
        public static partial Regex DbcNicknameTagRegex();
        #endregion

        #region Helper Functions
        public static IEnumerable<Unit> BuildCharacterDb()
        {
            Stream s = File.OpenRead("./wwwroot/data/DokkanCharacterData.json");

            var result = JsonSerializer.Deserialize<IEnumerable<Unit>>(s, InternalConstants.DefaultSerializeOptions);

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

        public static string CheckUsername(string username)
        {
            if (string.IsNullOrEmpty(username)) return username;

            var sub = InternalConstants.KnownUsernameMap.Keys.FirstOrDefault(x => Regex.IsMatch(username, x));

            if (!string.IsNullOrEmpty(sub))
            {
                return InternalConstants.KnownUsernameMap[sub];
            }
            else
            {
                Match m = DbcNicknameTagRegex().Match(username);

                if (!m.Success || m.Groups.Count < 2) return username;

                username = username.Replace(m.Groups[1].Value, OcrConstants.DbcTag);
            }

            return username;
        }
        #endregion

        #region Extension Methods
        public static WebhookMessage ToWebhookPayload(this Challenge challenge) => new()
        {
            Message = $"# Daily Challenge!\r\n{challenge.GetChallengeText(true)}!\r\n\r\n{InternalConstants.DokkandleDbcRole}\r\n\r\n*via https://dokkandle.net/daily*",
            FilePath = challenge.TodaysEvent?.WallpaperImagePath
        };

        public static string AddSasTokenToUri(this string uri, string sasToken)
            => $"{uri}?{sasToken}";

        public static string GetDisplayName(this LeaderboardUser leaderboardUser, bool usePingFormat = false) => string.IsNullOrWhiteSpace(leaderboardUser.DiscordUsername) ?
            leaderboardUser.DokkanNickname
            : usePingFormat ?
                $"<@{leaderboardUser.DiscordId}> ({leaderboardUser.DokkanNickname})"
                : $"{leaderboardUser.DiscordUsername} ({leaderboardUser.DokkanNickname})";

        public static string AddDokkandleDbcRolePing(this string source) => $"{source}\r\n{InternalConstants.DokkandleDbcRole}";

        public static string UnescapeUnicode(this string value) => string.IsNullOrWhiteSpace(value) ? value : Regex.Unescape(value);

        public static string EscapeUnicode(this string value)
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

        public static async Task<string> GetUsernameFromDiscordAuthClaim(this AuthenticationStateProvider authStateProvider)
        {
            var authState = await authStateProvider.GetAuthenticationStateAsync();
            var user = authState.User;

            if (!user.Claims.Any())
                return null;

            var claim = authState.User.Claims.FirstOrDefault(c => c.Issuer == "Discord" && c.Type.EndsWith("name"));

            return claim?.Value;
        }

        public static async Task<string> GetIdFromDiscordAuthClaim(this AuthenticationStateProvider authStateProvider)
        {
            var authState = await authStateProvider.GetAuthenticationStateAsync();
            var user = authState.User;

            if (!user.Claims.Any())
                return null;

            var claim = user.Claims.FirstOrDefault(c => c.Issuer == "Discord" && c.Type.Contains("identifier"));

            return claim?.Value;
        }

        public static bool IsAdministrator(this string id) => InternalConstants.Administrators.Contains(id);
    }
    #endregion
}
