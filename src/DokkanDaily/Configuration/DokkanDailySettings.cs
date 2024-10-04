namespace DokkanDaily.Configuration
{
    public class DokkanDailySettings
    {
        public string AzureBlobKey { get; init; }

        public string AzureBlobConnectionString { get; init; }

        public string AzureBlobContainerName { get; init; }

        public string AzureAccountName { get; init; }

        public string SqlServerConnectionString { get; init; }

        public string OAuth2ClientSecret { get; init; }

        public string OAuth2ClientId { get; init; }

        public FeatureFlags FeatureFlags { get; init; } = new();
    }
}
