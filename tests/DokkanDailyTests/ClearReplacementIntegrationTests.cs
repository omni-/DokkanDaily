using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using DokkanDaily.Configuration;
using DokkanDaily.Exceptions;
using DokkanDaily.Models;
using DokkanDaily.Models.Enums;
using DokkanDaily.Ocr;
using DokkanDaily.Services;
using DokkanDaily.Services.Interfaces;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

namespace DokkanDailyTests;

public class ClearReplacementIntegrationTests
{
    [Test]
    [Category("StorageIntegration")]
    public async Task UploadAndReplacementUseRealOcrAndConditionalBlobDeletes()
    {
        if (Environment.GetEnvironmentVariable("DOKKAN_TEST_AZURITE") != "1")
        {
            Assert.Ignore("Set DOKKAN_TEST_AZURITE=1 with local Azurite listening on port 10000.");
        }

        // Always use the local emulator and a container owned only by this test.
        string bucket = "replacement-test-" + Guid.NewGuid().ToString("N");
        BlobContainerClient container = new("UseDevelopmentStorage=true", bucket);
        DokkanDailySettings settings = new()
        {
            AzureBlobConnectionString = "UseDevelopmentStorage=true",
            AzureBlobContainerName = "replacement-test",
            FeatureFlags = new() { EnableJapaneseParsing = true }
        };
        Challenge challenge = new(DailyType.Category, new Stage("Unconfigured", Tier.Z, "test"),
            null, new Category("Test", Tier.Z), null, null, DateTime.Today);
        Mock<IRngHelperService> rng = new();
        rng.Setup(r => r.GetDailyChallenge()).ReturnsAsync(challenge);
        Mock<IUploadAttemptLimiter> admission = new();
        admission.Setup(a => a.TryAcceptAsync(null, "192.0.2.1"))
            .ReturnsAsync(new UploadAdmission(true, "ip:192.0.2.1", DateOnly.FromDateTime(challenge.Date)));
        OcrService ocr = new(NullLogger<OcrService>.Instance, Options.Create(settings), new());
        AzureBlobService service = new(Options.Create(settings), NullLogger<AzureBlobService>.Instance,
            ocr, rng.Object, admission.Object);
        string root = Path.GetFullPath(Path.Combine(TestContext.CurrentContext.TestDirectory, "../../../../.."));
        byte[] screenshot = await File.ReadAllBytesAsync(Path.Combine(root, "tests/DokkanDailyTests/Data/difficulty/heroine-global-super2-small.jpg"));
        Mock<IBrowserFile> file = new();
        file.Setup(f => f.OpenReadStream(It.IsAny<long>(), It.IsAny<CancellationToken>()))
            .Returns(() => new MemoryStream(screenshot));

        Task<ScreenshotUploadResult> Upload(Func<IEnumerable<string>, Task<bool>> confirm) =>
            service.UploadToAzureAsync("clear.jpg", "image/jpeg", file.Object, challenge,
                bucket: bucket, remoteIp: "192.0.2.1", confirmReplacement: confirm);

        try
        {
            ScreenshotUploadResult first = await Upload(_ => throw new AssertionException("First upload must not ask for replacement"));
            Assert.That((await first.Blob.DownloadContentAsync()).Value.Content.ToArray(), Is.EqualTo(screenshot));

            Assert.ThrowsAsync<UploadRejectedException>(() => Upload(names =>
            {
                Assert.That(names, Is.EqualTo(new[] { first.Blob.Name }));
                return Task.FromResult(false);
            }));
            Assert.That((await first.Blob.ExistsAsync()).Value, Is.True);

            ScreenshotUploadResult second = await Upload(names =>
            {
                Assert.That(names, Is.EqualTo(new[] { first.Blob.Name }));
                return Task.FromResult(true);
            });
            Assert.That(second.ReplacementIncomplete, Is.False);
            Assert.That(second.RemovedClearNames, Is.EqualTo(new[] { first.Blob.Name }));
            Assert.That((await first.Blob.ExistsAsync()).Value, Is.False);

            ScreenshotUploadResult third = await Upload(async names =>
            {
                Assert.That(names, Is.EqualTo(new[] { second.Blob.Name }));
                Response<BlobProperties> properties = await second.Blob.GetPropertiesAsync();
                Dictionary<string, string> changedMetadata = new(properties.Value.Metadata) { ["changed"] = "during-confirmation" };
                await second.Blob.SetMetadataAsync(changedMetadata);
                return true;
            });
            Assert.That(third.ReplacementIncomplete, Is.True);
            Assert.That(third.RemovedClearNames, Is.Empty);
            Assert.That((await third.Blob.DownloadContentAsync()).Value.Content.ToArray(), Is.EqualTo(screenshot));
            Assert.That((await second.Blob.GetPropertiesAsync()).Value.Metadata["changed"], Is.EqualTo("during-confirmation"));
            List<string> storedNames = [];
            await foreach (BlobItem blob in container.GetBlobsAsync())
            {
                storedNames.Add(blob.Name);
            }
            Assert.That(storedNames, Is.EquivalentTo(new[] { second.Blob.Name, third.Blob.Name }));
            await service.WaitForPendingAnalysis(TimeSpan.FromMilliseconds(10)).WaitAsync(TimeSpan.FromSeconds(5));
        }
        finally
        {
            await container.DeleteIfExistsAsync();
        }
    }
}
