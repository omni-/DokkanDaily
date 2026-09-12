using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using DokkanDaily.Constants;
using DokkanDaily.Exceptions;
using DokkanDaily.Helpers;
using DokkanDaily.Services;
using Moq;

namespace DokkanDailyTests;

public class ClearReplacementTests
{
    private static Dictionary<string, string> Identity(string ip, string name) => new()
    {
        [AzureConstants.IP_TAG] = ip,
        [AzureConstants.USER_NAME_TAG] = name.EscapeUnicode()
    };

    [TestCase("192.0.2.1", "Goku", true)]
    [TestCase("::ffff:192.0.2.1", "Goku", true)]
    [TestCase("192.0.2.2", "Goku", false)]
    [TestCase("192.0.2.1", "Vegeta", false)]
    [TestCase("192.0.2.1", null, false)]
    [TestCase(null, "Goku", false)]
    public void RequiresBothIpAndOcrName(string ip, string name, bool expected)
    {
        Assert.That(AzureBlobService.HasSameClearIdentity(Identity("192.0.2.1", "Goku"), ip, name), Is.EqualTo(expected));
    }

    [Test]
    public void MatchesStoredUnicodeName()
    {
        Assert.That(AzureBlobService.HasSameClearIdentity(Identity("192.0.2.1", "悟空"), "192.0.2.1", "悟空"), Is.True);
    }

    private static AzureBlobService CreateService() => new(
        Microsoft.Extensions.Options.Options.Create(new DokkanDaily.Configuration.DokkanDailySettings()),
        Microsoft.Extensions.Logging.Abstractions.NullLogger<AzureBlobService>.Instance, null, null, null);

    [Test]
    public async Task ResetCancelsUnansweredConfirmationAndDoesNotWaitForTheBrowser()
    {
        AzureBlobService service = CreateService();
        CancellationToken resetCancellation = await AzureBlobService.CaptureUploadCancellationAsync();
        Mock<BlobContainerClient> container = new(MockBehavior.Strict);
        BlobItem previous = BlobsModelFactory.BlobItem(name: "previous.png",
            metadata: Identity("192.0.2.1", "Goku"));
        Page<BlobItem> page = Page<BlobItem>.FromValues([previous], null, Mock.Of<Response>());
        container.Setup(c => c.GetBlobsAsync(BlobTraits.Metadata, BlobStates.None, null, default))
            .Returns(AsyncPageable<BlobItem>.FromPages([page]));
        TaskCompletionSource<bool> confirmation = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource dialogOpened = new(TaskCreationOptions.RunContinuationsAsynchronously);
        using MemoryStream stream = new([1, 2, 3]);
        Task<AzureBlobService.ClearStorageResult> store = AzureBlobService.StoreClearAsync(container.Object,
            "replacement.png", "image/png", stream, Identity("192.0.2.1", "Goku"), "2000-01-01",
            "192.0.2.1", "Goku", (_, token) =>
            {
                Assert.That(token, Is.EqualTo(resetCancellation));
                dialogOpened.SetResult();
                return confirmation.Task; // Deliberately ignore cancellation, like a missing browser response.
            }, resetCancellation: resetCancellation);
        await dialogOpened.Task.WaitAsync(TimeSpan.FromSeconds(5));

        await using (await service.AcquireResetBarrierAsync())
        {
            Assert.That(resetCancellation.IsCancellationRequested, Is.True);
            await service.WaitForPendingAnalysis(TimeSpan.FromMilliseconds(10)).WaitAsync(TimeSpan.FromSeconds(5));
            Assert.That(confirmation.Task.IsCompleted, Is.False);
            Assert.ThrowsAsync<UploadRejectedException>(async () => await AzureBlobService.CaptureUploadCancellationAsync());
            Assert.CatchAsync<OperationCanceledException>(async () => await store.WaitAsync(TimeSpan.FromSeconds(5)));
        }

        confirmation.SetResult(true);
        await confirmation.Task;
        container.Verify(c => c.GetBlobClient(It.IsAny<string>()), Times.Never);
        CancellationToken nextDay = await AzureBlobService.CaptureUploadCancellationAsync();
        Assert.That(nextDay.IsCancellationRequested, Is.False);
        Assert.That(resetCancellation.IsCancellationRequested, Is.True);
    }

    [Test]
    public async Task ConfirmationArrivingAfterResetCannotCommitToTheNewDay()
    {
        AzureBlobService service = CreateService();
        CancellationToken resetCancellation = await AzureBlobService.CaptureUploadCancellationAsync();
        Mock<BlobContainerClient> container = new(MockBehavior.Strict);
        BlobItem previous = BlobsModelFactory.BlobItem(name: "previous.png",
            metadata: Identity("192.0.2.1", "Goku"));
        Page<BlobItem> page = Page<BlobItem>.FromValues([previous], null, Mock.Of<Response>());
        container.Setup(c => c.GetBlobsAsync(BlobTraits.Metadata, BlobStates.None, null, default))
            .Returns(AsyncPageable<BlobItem>.FromPages([page]));
        using MemoryStream stream = new([1, 2, 3]);
        Assert.CatchAsync<OperationCanceledException>(async () =>
            await AzureBlobService.StoreClearAsync(container.Object, "replacement.png", "image/png", stream,
                Identity("192.0.2.1", "Goku"), "2000-01-01", "192.0.2.1", "Goku", async (_, _) =>
                {
                    await using (await service.AcquireResetBarrierAsync()) { }
                    return true;
                }, resetCancellation: resetCancellation));
        container.Verify(c => c.GetBlobClient(It.IsAny<string>()), Times.Never);
    }

    [Test]
    public async Task ResetDrainsAnAlreadyStartedStorageCommit()
    {
        AzureBlobService service = CreateService();
        CancellationToken token = await AzureBlobService.CaptureUploadCancellationAsync();
        Mock<BlobContainerClient> container = new();
        Mock<BlobClient> replacement = new();
        container.Setup(c => c.GetBlobClient("replacement.png")).Returns(replacement.Object);
        TaskCompletionSource<Response<BlobContentInfo>> upload = new(TaskCreationOptions.RunContinuationsAsynchronously);
        replacement.Setup(b => b.UploadAsync(It.IsAny<Stream>(), It.IsAny<BlobUploadOptions>(), default))
            .Returns(upload.Task);
        using MemoryStream stream = new([1, 2, 3]);
        Task<AzureBlobService.ClearStorageResult> store = AzureBlobService.StoreClearAsync(container.Object,
            "replacement.png", "image/png", stream, new Dictionary<string, string>(), "2000-01-01",
            null, null, null, resetCancellation: token);
        replacement.Verify(b => b.UploadAsync(It.IsAny<Stream>(), It.IsAny<BlobUploadOptions>(), default), Times.Once);

        await using (await service.AcquireResetBarrierAsync())
        {
            Task drain = service.WaitForPendingAnalysis(TimeSpan.FromSeconds(1));
            Assert.That(drain.IsCompleted, Is.False);
            upload.SetResult(Response.FromValue(BlobsModelFactory.BlobContentInfo(default, default, null, null, 0), Mock.Of<Response>()));
            await store.WaitAsync(TimeSpan.FromSeconds(5));
            await drain.WaitAsync(TimeSpan.FromSeconds(5));
        }
    }

    [TestCase(false, false, 0)]
    [TestCase(true, false, 0)]
    [TestCase(true, true, 0)]
    [TestCase(true, false, 404)]
    [TestCase(true, false, 412)]
    [TestCase(true, false, 500)]
    [TestCase(true, false, 0, false)]
    public async Task OnlyDeletesAfterConfirmationAndSuccessfulUpload(bool confirmed, bool uploadFails, int deleteStatus, bool hasEtag = true)
    {
        List<string> operations = [];
        Mock<BlobContainerClient> container = new Mock<BlobContainerClient>();
        Mock<BlobClient> previous = new Mock<BlobClient>();
        Mock<BlobClient> replacement = new Mock<BlobClient>();
        ETag etag = new ETag("previous-version");
        BlobItem previousItem = BlobsModelFactory.BlobItem(name: "previous.png",
            metadata: Identity("192.0.2.1", "Goku"),
            properties: BlobsModelFactory.BlobItemProperties(accessTierInferred: false, eTag: hasEtag ? etag : null));
        BlobItem otherItem = BlobsModelFactory.BlobItem(name: "other.png",
            metadata: Identity("192.0.2.1", "Vegeta"));
        Page<BlobItem> page = Page<BlobItem>.FromValues([previousItem, otherItem], null, Mock.Of<Response>());
        container.Setup(c => c.GetBlobsAsync(BlobTraits.Metadata, BlobStates.None, null, default))
            .Returns(AsyncPageable<BlobItem>.FromPages([page]));
        container.Setup(c => c.GetBlobClient("previous.png")).Returns(previous.Object);
        container.Setup(c => c.GetBlobClient("replacement.png")).Returns(replacement.Object);
        replacement.Setup(b => b.UploadAsync(It.IsAny<Stream>(), It.IsAny<BlobUploadOptions>(), default))
            .Returns(() =>
            {
                operations.Add("upload");
                if (uploadFails) throw new IOException("Upload failed");
                return Task.FromResult(Response.FromValue(BlobsModelFactory.BlobContentInfo(default, default, null, null, 0), Mock.Of<Response>()));
            });
        previous.Setup(b => b.DeleteAsync(DeleteSnapshotsOption.IncludeSnapshots,
                It.Is<BlobRequestConditions>(c => c.IfMatch == etag), default))
            .Callback(() => operations.Add("delete"))
            .Returns(() => deleteStatus == 0
                ? Task.FromResult(Mock.Of<Response>())
                : Task.FromException<Response>(new RequestFailedException(deleteStatus, "Delete failed")));
        using MemoryStream stream = new MemoryStream([1, 2, 3]);
        Task<AzureBlobService.ClearStorageResult> Store() => AzureBlobService.StoreClearAsync(container.Object, "replacement.png", "image/png",
            stream, Identity("192.0.2.1", "Goku"), "2000-01-01", "192.0.2.1", "Goku", (names, _) =>
            {
                Assert.That(names, Is.EqualTo(new[] { "previous.png" }));
                operations.Add("confirm");
                return Task.FromResult(confirmed);
            });

        if (!confirmed)
        {
            Assert.ThrowsAsync<UploadRejectedException>(async () => await Store());
            Assert.That(operations, Is.EqualTo(new[] { "confirm" }));
        }
        else if (uploadFails)
        {
            Assert.ThrowsAsync<IOException>(async () => await Store());
            Assert.That(operations, Is.EqualTo(new[] { "confirm", "upload" }));
        }
        else
        {
            AzureBlobService.ClearStorageResult result = await Store();
            Assert.That(result.Blob, Is.SameAs(replacement.Object));
            bool removed = hasEtag && deleteStatus is 0 or 404;
            Assert.That(result.ReplacementIncomplete, Is.EqualTo(!removed));
            Assert.That(result.RemovedClearNames, Is.EqualTo(removed ? new[] { "previous.png" } : Array.Empty<string>()));
            Assert.That(operations, Is.EqualTo(hasEtag ? new[] { "confirm", "upload", "delete" } : new[] { "confirm", "upload" }));
            // Storage must use the date selected with the container, not today's UTC date
            // after a potentially midnight-spanning confirmation.
            replacement.Verify(b => b.UploadAsync(It.IsAny<Stream>(),
                It.Is<BlobUploadOptions>(options => options.Tags[AzureConstants.DATE_TAG] == "2000-01-01"), default), Times.Once);
        }
        replacement.Verify(b => b.DeleteIfExistsAsync(It.IsAny<DeleteSnapshotsOption>(), It.IsAny<BlobRequestConditions>(), default), Times.Never);
        container.Verify(c => c.GetBlobClient("other.png"), Times.Never);
    }

    [TestCase(412)]
    [TestCase(500)]
    [TestCase(0)] // Lost response: deletion may have reached storage.
    public async Task ReportsPartialCleanupAndContinuesAfterAFailedDelete(int status)
    {
        Mock<BlobContainerClient> container = new();
        Mock<BlobClient> replacement = new();
        List<BlobItem> items = [];
        foreach (string name in new[] { "first.png", "failed.png", "last.png" })
        {
            ETag etag = new(name);
            items.Add(BlobsModelFactory.BlobItem(name: name, metadata: Identity("192.0.2.1", "Goku"),
                properties: BlobsModelFactory.BlobItemProperties(accessTierInferred: false, eTag: etag)));
            Mock<BlobClient> previous = new(MockBehavior.Strict);
            previous.Setup(b => b.DeleteAsync(DeleteSnapshotsOption.IncludeSnapshots,
                    It.Is<BlobRequestConditions>(c => c.IfMatch == etag), default))
                .Returns(() => name == "failed.png"
                    ? Task.FromException<Response>(status == 0 ? new IOException("Response lost") : new RequestFailedException(status, "Delete failed"))
                    : Task.FromResult(Mock.Of<Response>()));
            container.Setup(c => c.GetBlobClient(name)).Returns(previous.Object);
        }
        Page<BlobItem> page = Page<BlobItem>.FromValues(items, null, Mock.Of<Response>());
        container.Setup(c => c.GetBlobsAsync(BlobTraits.Metadata, BlobStates.None, null, default))
            .Returns(AsyncPageable<BlobItem>.FromPages([page]));
        container.Setup(c => c.GetBlobClient("replacement.png")).Returns(replacement.Object);
        replacement.Setup(b => b.UploadAsync(It.IsAny<Stream>(), It.IsAny<BlobUploadOptions>(), default))
            .ReturnsAsync(Response.FromValue(BlobsModelFactory.BlobContentInfo(default, default, null, null, 0), Mock.Of<Response>()));

        using MemoryStream stream = new([1, 2, 3]);
        AzureBlobService.ClearStorageResult result = await AzureBlobService.StoreClearAsync(container.Object,
            "replacement.png", "image/png", stream, Identity("192.0.2.1", "Goku"), "2000-01-01", "192.0.2.1", "Goku", (_, _) => Task.FromResult(true));

        Assert.That(result.Blob, Is.SameAs(replacement.Object));
        Assert.That(result.ReplacementIncomplete, Is.True);
        Assert.That(result.RemovedClearNames, Is.EqualTo(new[] { "first.png", "last.png" }));
        replacement.Verify(b => b.DeleteIfExistsAsync(It.IsAny<DeleteSnapshotsOption>(), It.IsAny<BlobRequestConditions>(), default), Times.Never);
    }
}
