using DokkanDaily.Configuration;
using DokkanDaily.Constants;
using DokkanDaily.Exceptions;
using DokkanDaily.Models;
using DokkanDaily.Models.Enums;
using DokkanDaily.Services;
using DokkanDaily.Services.Interfaces;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using System.Text.Json;

namespace DokkanDailyTests;

public class DifficultyTests
{
    private static string DataDirectory => Path.GetFullPath(Path.Combine(TestContext.CurrentContext.TestDirectory, "../../../Data"));

    private static IEnumerable<TestCaseData> Screenshots()
    {
        var expectations = JsonSerializer.Deserialize<Dictionary<string, string>>(
            File.ReadAllText(Path.Combine(DataDirectory, "difficulty/expectations.json")));
        return expectations.Select(kv => new TestCaseData(kv.Key, kv.Value).SetName($"Difficulty: {kv.Key}"));
    }

    [TestCaseSource(nameof(Screenshots))]
    public void ReadsDifficultyLabel(string relativePath, string expected)
    {
        using var logs = LoggerFactory.Create(builder => builder.SetMinimumLevel(LogLevel.Debug).AddSimpleConsole(options => options.SingleLine = true));
        var service = new OcrService(logs.CreateLogger<OcrService>(),
            Options.Create(new DokkanDailySettings { FeatureFlags = new() { EnableJapaneseParsing = true } }), new OcrFormatProvider());
        using var stream = new MemoryStream(File.ReadAllBytes(Path.Combine(DataDirectory, relativePath)));
        var result = service.ProcessImage(stream);
        Assert.That(result?.Difficulty, Is.EqualTo(expected));

        var stage = new Stage("Test stage", Tier.Z, "test", minimumDifficulty: StageDifficulty.SUPER3);
        if (expected == "SUPER3")
            Assert.DoesNotThrow(() => AzureBlobService.ValidateMinimumDifficulty(stage, result));
        else
            Assert.Throws<UploadRejectedException>(() => AzureBlobService.ValidateMinimumDifficulty(stage, result));
    }

    [TestCase(null, "SUPER3", true)]
    [TestCase(null, null, true)]
    [TestCase(StageDifficulty.SUPER2, "SUPER", false)]
    [TestCase(StageDifficulty.SUPER2, "SUPER2", true)]
    [TestCase(StageDifficulty.SUPER2, "SUPER3", true)]
    [TestCase(StageDifficulty.SUPER3, "SUPER2", false)]
    [TestCase(StageDifficulty.SUPER3, null, false)]
    [TestCase(StageDifficulty.SUPER3, "SUPERS", false)]
    [TestCase(StageDifficulty.SUPER3, "99", false)]
    [TestCase(StageDifficulty.SUPER3, "SUPER4", false)]
    public void EnforcesConfiguredMinimum(StageDifficulty? minimum, string label, bool accepted)
    {
        var stage = new Stage("Test stage", Tier.Z, "test", minimumDifficulty: minimum);
        var metadata = new ClearMetadata { Difficulty = label };
        if (accepted)
            Assert.DoesNotThrow(() => AzureBlobService.ValidateMinimumDifficulty(stage, metadata));
        else
            Assert.Throws<UploadRejectedException>(() => AzureBlobService.ValidateMinimumDifficulty(stage, metadata));
    }

    [TestCase("SUPER 2", "SUPER2")]
    [TestCase("super3\n", "SUPER3")]
    [TestCase("Z-HARD", "ZHARD")]
    [TestCase("HARD", "HARD")]
    [TestCase("NORMAL", "NORMAL")]
    [TestCase("SUPER2 extra", null)]
    [TestCase("3", null)]
    [TestCase("SUPER4", null)]
    [TestCase(null, null)]
    public void NormalizesOnlyRecognizedLabels(string text, string expected) =>
        Assert.That(OcrService.NormalizeDifficulty(text), Is.EqualTo(expected));

    [Test]
    public void StoresRecognizedDifficultyAndCannotFinalizeALowerClear()
    {
        var stage = new Stage("Test stage", Tier.Z, "test", minimumDifficulty: StageDifficulty.SUPER3);
        var challenge = new Challenge(DailyType.Category, stage, null, new Category("Test", Tier.Z), null, null, DateTime.Today);
        var tags = AzureBlobService.BuildTagDict(challenge, new ClearMetadata
        {
            Difficulty = "SUPER3", Nickname = "Tester", ClearTime = "0'01\"00.0", ItemlessClear = true
        }, null, null, null, null);
        Assert.Multiple(() =>
        {
            Assert.That(tags[AzureConstants.DIFFICULTY_TAG], Is.EqualTo("SUPER3"));
            Assert.That(tags[AzureConstants.UPLOAD_STATUS_TAG], Is.EqualTo(AzureConstants.UPLOAD_STATUS_VALID));
        });
        Assert.Throws<UploadRejectedException>(() => AzureBlobService.BuildTagDict(challenge,
            new ClearMetadata { Difficulty = "SUPER2" }, null, null, null, null));
        Assert.Throws<UploadRejectedException>(() => AzureBlobService.BuildTagDict(challenge, null, null, null, null, null));
    }

    [TestCase("SUPER2")]
    [TestCase(null)]
    public async Task RejectsBeforeStorageUsingServerRequirementAndReleasesPendingAnalysis(string label)
    {
        var stage = new Stage("Test stage", Tier.Z, "test", minimumDifficulty: StageDifficulty.SUPER3);
        var current = new Challenge(DailyType.Category, stage, null, new Category("Test", Tier.Z), null, null, DateTime.Today);
        // An old/client-supplied challenge without a minimum must not bypass the gate.
        var submitted = new Challenge(DailyType.Category, new Stage("Test stage", Tier.Z, "test"),
            null, current.Category, null, null, current.Date);
        var rng = new Mock<IRngHelperService>();
        rng.Setup(x => x.GetDailyChallenge()).ReturnsAsync(current);
        var admission = new Mock<IUploadAttemptLimiter>();
        admission.Setup(x => x.TryAcceptAsync("123", null))
            .ReturnsAsync(new UploadAdmission(true, "discord:123", DateOnly.FromDateTime(current.Date)));
        var ocr = new Mock<IOcrService>();
        ocr.Setup(x => x.ProcessImage(It.IsAny<MemoryStream>()))
            .Returns(new ClearMetadata { Difficulty = label });
        var file = new Mock<IBrowserFile>();
        file.Setup(x => x.OpenReadStream(It.IsAny<long>(), It.IsAny<CancellationToken>()))
            .Returns(() => new MemoryStream([1, 2, 3]));
        var service = new AzureBlobService(Options.Create(new DokkanDailySettings
        {
            // Reaching storage would fail before any network call, rather than using real credentials.
            AzureBlobConnectionString = "intentionally invalid", AzureBlobContainerName = "test"
        }), NullLogger<AzureBlobService>.Instance, ocr.Object, rng.Object, admission.Object);

        var error = Assert.ThrowsAsync<UploadRejectedException>(() => service.UploadToAzureAsync(
            "clear.png", "image/png", file.Object, submitted, discordId: "123"));
        Assert.That(error.Message, Does.Contain("SUPER3"));
        ocr.Verify(x => x.ProcessImage(It.IsAny<MemoryStream>()), Times.Once);
        await service.WaitForPendingAnalysis(TimeSpan.FromMilliseconds(50)).WaitAsync(TimeSpan.FromSeconds(5));
    }
}

