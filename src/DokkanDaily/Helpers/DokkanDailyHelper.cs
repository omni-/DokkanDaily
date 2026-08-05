using DokkanDaily.Constants;
using DokkanDaily.Models;
using DokkanDaily.Services;
using Microsoft.AspNetCore.Components.Authorization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace DokkanDaily.Helpers
{
    public static partial class DokkanDailyHelper
    {
        private const int MaxBlobNameLength = 200;

        #region Regex
        [GeneratedRegex("[^a-zA-Z0-9-]")]
        public static partial Regex AlphaNumericRegex();

        [GeneratedRegex(@"([UDO]BC\s?[\*\+]\s?).*")]
        public static partial Regex DbcNicknameTagRegex();

        [GeneratedRegex("[^a-zA-Z0-9._-]")]
        public static partial Regex UnsafeBlobNameCharRegex();
        #endregion

        #region Helper Functions
        public static IEnumerable<Unit> BuildCharacterDb()
        {
            using Stream s = File.OpenRead("./wwwroot/data/DokkanCharacterData.json");

            IEnumerable<Unit> result = JsonSerializer.Deserialize<IEnumerable<Unit>>(s, InternalConstants.DefaultSerializeOptions);

            foreach (var unit in result)
                if (unit.ImageUrl.Contains("static."))
                    unit.ImageUrl = unit.ImageUrl.Replace("static.", "vignette.");

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

        /// <summary>
        /// The virtual directory a logged in user's clears live under, or <see langword="null"/> for
        /// an anonymous upload. Includes the trailing slash so it can be used as a blob prefix.
        /// </summary>
        public static string GetUserBlobPrefix(string discordId)
            => string.IsNullOrWhiteSpace(discordId) ? null : $"{AlphaNumericRegex().Replace(discordId, "")}/";

        /// <summary>
        /// Builds a storage-safe, unique blob name from a client-supplied file name.
        /// </summary>
        /// <remarks>
        /// Path separators and other unsafe characters are stripped, and a GUID is appended so two
        /// uploads can never collide. The uploader's Discord id, when known, scopes the blob into
        /// its own virtual directory, which keeps one user from touching another's clears at all.
        /// The user agent deliberately plays no part in the name - it used to, which meant
        /// re-uploading a previously downloaded clear stacked a second agent onto the name. It is
        /// recorded in blob metadata instead.
        /// </remarks>
        public static string BuildBlobName(string userFileName, string discordId)
        {
            // Path.GetFileName only recognizes the current platform's directory separator. Browser
            // file names can contain either style, so normalize Windows separators before taking
            // the leaf name to keep traversal components out on Linux as well as Windows.
            string leafName = Path.GetFileName((userFileName ?? "").Replace('\\', '/'));
            string ext = UnsafeBlobNameCharRegex().Replace(Path.GetExtension(leafName), "");
            string name = UnsafeBlobNameCharRegex().Replace(Path.GetFileNameWithoutExtension(leafName), "").Trim('.');

            if (string.IsNullOrWhiteSpace(name)) name = "clear";
            if (name.Length > MaxBlobNameLength) name = name[..MaxBlobNameLength];

            return $"{GetUserBlobPrefix(discordId)}{name}-{Guid.NewGuid():N}{ext}";
        }

        public static Unit GetUnit(string name, string title)
            => DokkanConstants.UnitDB.First(x => x.Name == name && x.Title == title);

        public static Unit GetUnit(Leader leader)
            => DokkanConstants.UnitDB.First(x => x.Name == leader.Name && x.Title == leader.Title);

        /// <summary>
        /// Resolves the unit backing a leader, returning <see langword="null"/> when the leader is
        /// unset or has no matching entry in the character database.
        /// </summary>
        public static Unit GetUnitOrDefault(Leader leader)
            => leader is null ? null : DokkanConstants.UnitDB.FirstOrDefault(x => x.Name == leader.Name && x.Title == leader.Title);

        public static string FixUsername(string username)
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
            Message = challenge is null ? $"Oops! Bot died! Someone ping {InternalConstants.Owner}!" : $"# Daily Challenge!\r\n{challenge.GetChallengeText(true)}!\r\n\r\n{InternalConstants.DokkandleDbcRole}\r\n\r\n*via https://dokkandle.net/daily*",
            FilePath = challenge?.TodaysEvent?.WallpaperImagePath
        };

        public static string GetDisplayName(this LeaderboardUser leaderboardUser, bool usePingFormat = false) => string.IsNullOrWhiteSpace(leaderboardUser.DiscordUsername) ?
            leaderboardUser.DokkanNickname
            : usePingFormat && !string.IsNullOrWhiteSpace(leaderboardUser.DiscordId) ?
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

        public static bool IsDuringReset(this TimeOnly targetUtcTime, TimeOnly now)
        {
            var end = targetUtcTime.Add(TimeSpan.FromSeconds(Worker.ResetDuration));

            if (targetUtcTime <= end)
                return now >= targetUtcTime && now < end;

            return now >= targetUtcTime || now < end;
        }

    }
    #endregion
}
