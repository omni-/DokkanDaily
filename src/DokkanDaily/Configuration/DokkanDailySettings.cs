namespace DokkanDaily.Configuration
{
    public class DokkanDailySettings
    {
        public string AzureBlobConnectionString { get; init; }

        // Keep these stable across instances/restarts; isolate environments with separate containers.
        public string DataProtectionContainerName { get; init; } = "data-protection";
        public string DataProtectionApplicationName { get; init; } = "DokkanDaily";

        public string AzureBlobContainerName { get; init; }

        public string SqlServerConnectionString { get; init; }

        public string OAuth2ClientSecret { get; init; }

        public string OAuth2ClientId { get; init; }

        public string WebhookUrl { get; init; }

        public int StageRepeatLimitDays { get; init; }

        public int EventRepeatLimitDays { get; init; }

        public FeatureFlags FeatureFlags { get; init; } = new();
    }
}
