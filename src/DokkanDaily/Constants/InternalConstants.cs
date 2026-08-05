using System.Text.Json;

namespace DokkanDaily.Constants
{
    public static class InternalConstants
    {
        public static string DokkandleDbcRole => "<@&1289820573949497345>";

        public static readonly Dictionary<string, string> KnownUsernameMap = new()
        {
            // pattern, value
            { "五.悟", "五条悟" },
            { "UBCeomnt", "DBC*omni" },
            { "Komacni", "Komachi" },
            { "Komacnhi", "Komachi" },
            { "\\*FIGOe", "*FIGO*" }
        };

        public static readonly JsonSerializerOptions DefaultSerializeOptions = new() { PropertyNameCaseInsensitive = true };

        public static IReadOnlyList<string> Administrators => _administrators.AsReadOnly();

        public static string Owner => $"<@{_administrators[0]}>";

        private static readonly List<string> _administrators = ["112089455933792256", "263499818234675200"];

        public static DateTime Season1StartDate => new(2025, 1, 1);

        public static int ChallengeRepeatLimitDays => 7;

        /// <summary>
        /// How long the reset will wait for OCR of last-minute uploads to finish before reading
        /// clears. Anything still pending after this is skipped, so it trades reset latency against
        /// dropping a clear that was accepted before the deadline.
        /// </summary>
        public static TimeSpan PendingOcrDrainTimeout => TimeSpan.FromSeconds(45);
    }
}
