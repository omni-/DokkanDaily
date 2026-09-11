using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using DokkanDaily.Configuration;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace DokkanDailyTests;

public class DataProtectionTests
{
    [Test]
    public void MissingProductionStorageFailsInsteadOfUsingLocalKeys()
    {
        Assert.Throws<InvalidOperationException>(() => DataProtectionConfiguration.Configure(new ServiceCollection(), new(), false));
        Assert.DoesNotThrow(() => DataProtectionConfiguration.Configure(new ServiceCollection(), new(), true));
    }

    [Test]
    public async Task SharedBlobKeysSurviveProviderRecreationAndRejectUnsafeStorage()
    {
        string connection = Environment.GetEnvironmentVariable("DOKKAN_TEST_BLOB_CONNECTION");
        if (string.IsNullOrEmpty(connection)) Assert.Ignore("Set DOKKAN_TEST_BLOB_CONNECTION to an isolated Azurite instance.");
        string containerName = "key-test-" + Guid.NewGuid().ToString("N");
        var container = new BlobContainerClient(connection, containerName);
        var settings = new DokkanDailySettings { AzureBlobConnectionString = connection, DataProtectionContainerName = containerName };
        ServiceProvider Build()
        {
            var services = new ServiceCollection().AddLogging();
            DataProtectionConfiguration.Configure(services, settings, false);
            return services.BuildServiceProvider();
        }
        try
        {
            string encrypted;
            using (var first = Build())
            {
                foreach (var check in first.GetServices<IHostedService>()) await check.StartAsync(default);
                encrypted = first.GetRequiredService<IDataProtectionProvider>().CreateProtector("test-purpose").Protect("shared-cookie");
                using var second = Build();
                Assert.That(second.GetRequiredService<IDataProtectionProvider>().CreateProtector("test-purpose").Unprotect(encrypted), Is.EqualTo("shared-cookie"));
            }
            using (var restarted = Build())
                Assert.That(restarted.GetRequiredService<IDataProtectionProvider>().CreateProtector("test-purpose").Unprotect(encrypted), Is.EqualTo("shared-cookie"));
            Assert.That((await container.GetBlobClient("keys.xml").ExistsAsync()).Value, Is.True);
            await container.SetAccessPolicyAsync(PublicAccessType.Blob);
            Assert.Throws<InvalidOperationException>(() => { using var rejected = Build(); });
            await container.SetAccessPolicyAsync(PublicAccessType.None);
            await container.GetBlobClient("keys.xml").UploadAsync(BinaryData.FromString("invalid-key-ring"), overwrite: true);
            using var broken = Build();
            Assert.CatchAsync<System.Security.Cryptography.CryptographicException>(async () =>
            {
                foreach (var check in broken.GetServices<IHostedService>()) await check.StartAsync(default);
            });
            Assert.That((await container.GetBlobClient("keys.xml").DownloadContentAsync()).Value.Content.ToString(), Is.EqualTo("invalid-key-ring"));
        }
        finally { await container.DeleteIfExistsAsync(); }
    }
}
