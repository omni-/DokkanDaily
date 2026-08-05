namespace DokkanDaily.Constants
{
    public static class AzureConstants
    {
        public static string IP_TAG => "ip";

        public static string DATE_TAG => "date";

        public static string USER_NAME_TAG => "username";

        public static string DAILY_TYPE_TAG => "dailytype";

        public static string EVENT_TAG => "event";

        public static string CLEAR_TIME_TAG => "cleartime";

        public static string ITEMLESS_TAG => "itemless";

        public static string DISCORD_NAME_TAG => "discordusername";

        public static string INVALID_TAG => "invalidrun";

        public static string CHALLENGE_TARGET_TAG => "challengetarget";

        public static string DISCORD_ID => "discordid";

        public static string USER_AGENT_TAG => "useragent";

        /// <summary>
        /// Container holding the Data Protection key ring. Deliberately has no date suffix, so the
        /// prune job's <c>{container}-{MM-dd-yyyy}</c> filter can never match and delete it.
        /// </summary>
        public const string DATA_PROTECTION_CONTAINER = "dataprotection-keys";

        public const string DATA_PROTECTION_BLOB = "keys.xml";

        /// <summary>
        /// Purpose string mixed into every protected payload. Must stay identical across instances
        /// and deployment slots: changing it, or letting it default to the assembly name in one
        /// place and not another, invalidates every cookie issued by the other side.
        /// </summary>
        public const string DATA_PROTECTION_APP_NAME = "DokkanDaily";
    }
}
