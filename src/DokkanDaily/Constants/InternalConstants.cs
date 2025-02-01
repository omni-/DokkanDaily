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
            { "UBCeomnt", "DBC*omni" }
        };

        public static readonly JsonSerializerOptions DefaultSerializeOptions = new() { PropertyNameCaseInsensitive = true };

        public static IReadOnlyList<string> Administrators => _administrators.AsReadOnly();

        private static readonly List<string> _administrators = ["112089455933792256", "263499818234675200"];

        public static DateTime Season1StartDate => new(2025, 1, 1);
    }
}
