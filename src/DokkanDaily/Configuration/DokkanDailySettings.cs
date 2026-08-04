namespace DokkanDaily.Configuration
{
    public class DokkanDailySettings
    {
        /// <summary>
        /// Standard storage connection string. It carries the account name and key, which is why
        /// neither is configured separately - the blob clients sign their own read SAS from it.
        /// </summary>
        public string AzureBlobConnectionString { get; init; }

        public string AzureBlobContainerName { get; init; }

        public string SqlServerConnectionString { get; init; }

        public string OAuth2ClientSecret { get; init; }

        public string OAuth2ClientId { get; init; }

        public string WebhookUrl { get; init; }

        // defaulted so that a missing configuration key cannot silently disable repeat protection
        public int StageRepeatLimitDays { get; init; } = 30;

        public int EventRepeatLimitDays { get; init; } = 7;

        public FeatureFlags FeatureFlags { get; init; } = new();
    }
}
