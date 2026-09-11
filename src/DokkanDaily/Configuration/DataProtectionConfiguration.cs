using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.AspNetCore.DataProtection;

namespace DokkanDaily.Configuration;

internal static class DataProtectionConfiguration
{
    internal static void Configure(IServiceCollection services, DokkanDailySettings settings, bool isDevelopment)
    {
        if (string.IsNullOrWhiteSpace(settings.DataProtectionApplicationName))
            throw new InvalidOperationException("DataProtectionApplicationName must be configured.");
        var protection = services.AddDataProtection().SetApplicationName(settings.DataProtectionApplicationName);
        if (string.IsNullOrWhiteSpace(settings.AzureBlobConnectionString))
        {
            if (!isDevelopment)
                throw new InvalidOperationException("AzureBlobConnectionString is required for shared Data Protection keys outside Development.");
            return;
        }

        if (string.IsNullOrWhiteSpace(settings.DataProtectionContainerName))
            throw new InvalidOperationException("DataProtectionContainerName must be configured.");
        var container = new BlobContainerClient(settings.AzureBlobConnectionString, settings.DataProtectionContainerName);
        // Fail startup on unavailable or public storage; never fork a configured shared key ring.
        container.CreateIfNotExists(PublicAccessType.None);
        if (container.GetAccessPolicy().Value.BlobPublicAccess != PublicAccessType.None)
            throw new InvalidOperationException("The Data Protection container must be private.");
        protection.PersistKeysToAzureBlobStorage(container.GetBlobClient("keys.xml"));
        services.AddHostedService<KeyRingStartupCheck>();
    }

    private sealed class KeyRingStartupCheck(IDataProtectionProvider provider) : IHostedService
    {
        public Task StartAsync(CancellationToken cancellationToken)
        {
            var protector = provider.CreateProtector("DokkanDaily.KeyRingStartupCheck");
            const string probe = "key-ring-ready";
            if (protector.Unprotect(protector.Protect(probe)) != probe)
                throw new InvalidOperationException("Data Protection key ring validation failed.");
            return Task.CompletedTask;
        }
        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
