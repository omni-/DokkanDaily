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

    [TestCase(false, false, 0)]
    [TestCase(true, false, 0)]
    [TestCase(true, true, 0)]
    [TestCase(true, false, 412)]
    [TestCase(true, false, 500)]
    public async Task OnlyDeletesAfterConfirmationAndSuccessfulUpload(bool confirmed, bool uploadFails, int deleteStatus)
    {
        List<string> operations = [];
        var container = new Mock<BlobContainerClient>();
        var previous = new Mock<BlobClient>();
        var replacement = new Mock<BlobClient>();
        var etag = new ETag("previous-version");
        var previousItem = BlobsModelFactory.BlobItem(name: "previous.png",
            metadata: Identity("192.0.2.1", "Goku"),
            properties: BlobsModelFactory.BlobItemProperties(accessTierInferred: false, eTag: etag));
        var otherItem = BlobsModelFactory.BlobItem(name: "other.png",
            metadata: Identity("192.0.2.1", "Vegeta"));
        var page = Page<BlobItem>.FromValues([previousItem, otherItem], null, Mock.Of<Response>());
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
        replacement.Setup(b => b.DeleteIfExistsAsync(DeleteSnapshotsOption.IncludeSnapshots, null, default))
            .Callback(() => operations.Add("rollback"))
            .ReturnsAsync(Response.FromValue(true, Mock.Of<Response>()));

        using var stream = new MemoryStream([1, 2, 3]);
        Task<BlobClient> Store() => AzureBlobService.StoreClearAsync(container.Object, "replacement.png", "image/png",
            stream, Identity("192.0.2.1", "Goku"), "192.0.2.1", "Goku", names =>
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
        else if (deleteStatus != 0)
        {
            var error = Assert.ThrowsAsync<UploadRejectedException>(async () => await Store());
            if (deleteStatus == 412)
            {
                Assert.That(operations, Is.EqualTo(new[] { "confirm", "upload", "delete", "rollback" }));
                Assert.That(error.Message, Does.Contain("this upload was canceled"));
            }
            else
            {
                Assert.That(operations, Is.EqualTo(new[] { "confirm", "upload", "delete" }));
                Assert.That(error.Message, Does.Contain("new clear was saved"));
                Assert.That(error.Message, Does.Contain("You do not need to upload again"));
            }
        }
        else
        {
            Assert.That(await Store(), Is.SameAs(replacement.Object));
            Assert.That(operations, Is.EqualTo(new[] { "confirm", "upload", "delete" }));
        }
        container.Verify(c => c.GetBlobClient("other.png"), Times.Never);
    }
}
