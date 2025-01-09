namespace DokkanDaily.Constants
{
    public class InternalConstants
    {
        public static string DokkandleDbcRole => "<@&1289820573949497345>";

        public static int ChallengeRepeatLimitDays => 30;

        public static int EventRepeatLimitDays => 7;

        public static DateTime Season1StartDate => new(2025, 1, 1);

        public static readonly Dictionary<string, string> KnownUsernameMap = new() 
        {
            // pattern, value
            {"五.悟", "五条悟"} 
        };
    }
}
