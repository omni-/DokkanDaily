namespace DokkanDaily.Configuration
{
    public class DokkanDailySettings
    {
        public int SeedOffset { get; init; }

        public string AzureBlobKey { get; init; }

        public string AzureBlobConnectionString { get; init; }

        public string AzureBlobContainerName { get; init; }

        public string AzureAccountName { get; init; }
    }
}
