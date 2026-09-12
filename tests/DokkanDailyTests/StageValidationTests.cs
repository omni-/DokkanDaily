using System.Security.Cryptography;
using System.Text.Json;
using DokkanDaily.Configuration;
using DokkanDaily.Constants;
using DokkanDaily.Exceptions;
using DokkanDaily.Models;
using DokkanDaily.Models.Enums;
using DokkanDaily.Ocr;
using DokkanDaily.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.AspNetCore.Components.Forms;
using DokkanDaily.Services.Interfaces;
using Moq;

namespace DokkanDailyTests;

public class StageValidationTests
{
    private static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true };
    private static string Root => Path.GetFullPath(Path.Combine(TestContext.CurrentContext.TestDirectory, "../../../../.."));
    private static string Fixtures => Path.Combine(Root, "tests/DokkanDailyTests/Data/stage-validation");
    private static StageTextTarget Target(string e, string s) => new() { Aliases = [new(e, s)], MinimumDifficulty = "SUPER2" };

    [TestCase("Movie Battle", "Vs. Gogeta", "Movie Battle", "Vs. Gogeta", "match")]
    [TestCase("Ultimate Red Zone Movie Editlon", "Vs. Super Janemha", "Ultimate Red Zone Movie Edition", "Vs. Super Janemba", "match")]
    [TestCase("Movie Battle", "Vs. Super Saiyan Gohan", "Movie Battle", "Vs. Super Saiyan Goku", "unknown")]
    [TestCase("Movie Battle", "Vs. Super Saiyan Goku", "Movie Battle", "Vs. Super Saiyan God Goku", "unknown")]
    [TestCase("Supreme Magnificent Battle", "Vs. Goku", "Supreme Magnificent Battle RE", "Vs. Goku", "unknown")]
    [TestCase("Special Battle", "Stage 2", "Special Battle", "Stage 1", "stage-mismatch")]
    [TestCase("Special Battle", "ステージ11", "Special Battle", "ステージ1", "stage-mismatch")]
    [TestCase("Special Battle", "Stage I", "Special Battle", "Stage 1", "unknown")]
    [TestCase("Special Battle", "Sta9e 1", "Special Battle", "Stage 1", "unknown")]
    [TestCase("ドキ ドキ ヒロイン バトル", "ｖｓ 人造人間１８号", "ドキドキ♥ヒロインバトル", "VS人造人間18号", "match")]
    [TestCase(null, "Vs. Gogeta", "Movie Battle", "Vs. Gogeta", "unknown")]
    [TestCase("Movie Battle", "????", "Movie Battle", "Vs. Gogeta", "unknown")]
    public void PreservesFrozenTextRules(string observedEvent, string observedStage, string targetEvent, string targetStage, string expected) =>
        Assert.That(StageTextValidator.Validate(new(observedEvent, observedStage, "SUPER3"), Target(targetEvent, targetStage)).Outcome, Is.EqualTo(expected));

    [TestCase("Vs. Super Saiyan Goku", "Vs. Super Saiyan Gohan")]
    [TestCase("Vs. Android 18", "Vs. Android 17")]
    [TestCase("VS超サイヤ人孫悟空", "VS超サイヤ人孫悟飯")]
    public void KnownNeighborWins(string targetName, string otherName)
    {
        var target = Target("Movie Battle", targetName);
        Assert.That(StageTextValidator.Validate(new("Movie Battle", otherName, "SUPER3"), target, [target, Target("Movie Battle", otherName)]).Outcome, Is.EqualTo("stage-mismatch"));
    }

    [Test]
    public void IdenticalNamesDoNotProveInternalIds()
    {
        var first = Target("Movie Battle", "Vs. Goku") with { EventId = 796, StageNumber = 1 };
        var second = first with { StageNumber = 5 };
        Assert.That(StageTextValidator.Validate(new("Movie Battle", "Vs. Goku", "SUPER3"), first, [first, second]).Outcome, Is.EqualTo("match"));
    }

    [TestCase("SUPER", "difficulty-mismatch")]
    [TestCase("SUPER3", "match")]
    [TestCase(null, "unknown")]
    [TestCase("SUPER?", "unknown")]
    public void DifficultyRemainsIndependent(string label, string expected) =>
        Assert.That(StageTextValidator.Validate(new("Movie Battle", "Vs. Goku", label), Target("Movie Battle", "Vs. Goku")).Outcome, Is.EqualTo(expected));

    [Test]
    public void MissingLocalizationAndLayoutRemainUnknown()
    {
        var en = Target("Fearsome Activation! Cell Max", "Horrendous Calamity") with { EventId = 761 };
        var ja = Target("目醒める恐怖!セルマックス", "目醒める恐怖!セルマックス") with { EventId = 761 };
        Assert.Multiple(() =>
        {
            Assert.That(StageTextValidator.Validate(new("目醒める恐怖!セルマックス", "未知の日本語ステージ", "SUPER3"), en, [en, ja]).Outcome, Is.EqualTo("unknown"));
            Assert.That(StageTextValidator.Validate(new("Movie Battle", "Vs. Goku", "SUPER3"), new()).Reason, Is.EqualTo("missing-target-localized-names"));
            Assert.That(StageTextValidator.Validate(new("Movie Battle", "Vs. Goku", "SUPER3", false), Target("Movie Battle", "Vs. Goku")).Outcome, Is.EqualTo("unknown"));
        });
    }

    [Test]
    public void KnownWrongEventWinsOverSharedStageName()
    {
        var target = Target("Movie Battle", "Vs. Goku");
        var neighbor = Target("Heroine Battle", "Vs. Goku");
        Assert.That(StageTextValidator.Validate(new("Heroine Battle", "Vs. Goku", "SUPER3"), target, [target, neighbor]).Outcome, Is.EqualTo("event-mismatch"));
    }

    [Test]
    public void KnownEditionAndTiedNeighborEvidenceStayDistinct()
    {
        var edition = Target("Supreme Magnificent Battle RE", "Vs. Goku");
        var old = Target("Supreme Magnificent Battle", "Vs. Goku");
        Assert.That(StageTextValidator.Validate(new(old.Aliases.First().EventTitle, "Vs. Goku", "SUPER3"), edition, [edition, old]).Outcome, Is.EqualTo("event-mismatch"));
        var target = Target("Movie Battle", "Vs. Goku");
        Assert.That(StageTextValidator.Validate(new("Movie Battle", "Vs. Gokx", "SUPER3"), target, [target, Target("Movie Battle", "Vs. Goki")]).Reason, Is.EqualTo("indistinguishable-ocr-alternatives"));
    }

    [Test]
    public void CatalogEnrichesExistingStagesWithoutInventingMissingNames()
    {
        var stages = DokkanConstants.Stages.DistinctBy(s => s.FullName).ToArray();
        var covered = stages.Where(s => StageTitleCatalog.ForStage(s).Aliases.Any(a =>
            !string.IsNullOrWhiteSpace(a.EventTitle) && !string.IsNullOrWhiteSpace(a.StageTitle))).ToArray();
        Assert.Multiple(() =>
        {
            Assert.That(covered, Is.EquivalentTo(stages), "Every configured stage must have sourced title aliases; run scripts/sync-stage-links.py");
            Assert.That(StageTitleCatalog.ForStage(new("Unconfigured", Tier.Z, "test")).Aliases, Is.Empty);
        });
    }

    [TestCase("Z-HARD", "Z-HARD", "match")]
    [TestCase("SUPER3", null, "match")]
    [TestCase(null, null, "unknown")]
    [TestCase("SUPER3", "SUPER4", "unknown")]
    public void FrozenDifficultyVocabulary(string observed, string minimum, string expected) =>
        Assert.That(StageTextValidator.Validate(new("Movie Battle", "Vs. Goku", observed), Target("Movie Battle", "Vs. Goku") with { MinimumDifficulty = minimum }).Outcome, Is.EqualTo(expected));

    [TestCase("Straße", "STRASSE")]
    [TestCase("ς", "Σ")]
    [TestCase("ｖｓ人造人間１８号", "VS人造人間18号")]
    public void UsesUnicodeCaseFolding(string observed, string expected) =>
        Assert.That(StageTextValidator.Normalize(observed), Is.EqualTo(StageTextValidator.Normalize(expected)));

    [Test]
    public void PackagedCandidateModelIsFrozen()
    {
        string path = Path.Combine(Root, "src/DokkanDaily/wwwroot/tessdata/jpn-stage-v2.traineddata");
        Assert.That(Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(path))), Is.EqualTo("0bf65e5fe79140725174ca41d4f1a193c8f2232224e9df31dba657cf928fdf5e"));
    }

    [Test]
    public void UploadUsesExistingStageAndPreservesUnknown()
    {
        var stage = DokkanConstants.Stages.First(s => s.Name == "Fearsome Activation! Cell Max" && s.StageNumber == 2);
        var challenge = new Challenge(DailyType.Category, stage, null, new Category("Test", Tier.Z), null, null, DateTime.Today);
        Dictionary<string, string> Tags(ClearMetadata m) => AzureBlobService.BuildTagDict(challenge, m, null, null, null, null);
        var valid = Tags(new() { EventTitle = stage.Name, StageTitle = "Horrendous Calamity", Difficulty = "SUPER3" });
        Assert.That(valid[AzureConstants.UPLOAD_STATUS_TAG], Is.EqualTo("valid"));
        Assert.Throws<UploadRejectedException>(() => Tags(new() { EventTitle = stage.Name, StageTitle = stage.Name, Difficulty = "SUPER3" }));
        var unknown = Tags(new() { Difficulty = "SUPER3" });
        Assert.Multiple(() =>
        {
            Assert.That(unknown[AzureConstants.UPLOAD_STATUS_TAG], Is.EqualTo("valid"));
            Assert.That(unknown[AzureConstants.STAGE_VALIDATION_TAG], Is.EqualTo("unknown"));
            Assert.That(unknown.ContainsKey(AzureConstants.INVALID_TAG), Is.False);
            Assert.That(Tags(null)[AzureConstants.UPLOAD_STATUS_TAG], Is.EqualTo("valid"));
        });
    }

    [Test]
    public void MissingCatalogNamesDoNotWithholdUploads()
    {
        var stage = new Stage("Unconfigured future event", Tier.Z, "test");
        var challenge = new Challenge(DailyType.Category, stage, null, new Category("Test", Tier.Z), null, null, DateTime.Today);
        var tags = AzureBlobService.BuildTagDict(challenge, new() { Difficulty = "SUPER3" }, null, null, null, null);
        Assert.That(tags[AzureConstants.UPLOAD_STATUS_TAG], Is.EqualTo("valid"));
        Assert.That(tags[AzureConstants.STAGE_VALIDATION_REASON_TAG], Is.EqualTo("missing-target-localized-names"));
    }

    [TestCase(1, "Saiyan Saga", "Planet Namek Saga")]
    [TestCase(2, "Planet Namek Saga", "Saiyan Saga")]
    public void CollectionOfEpicBattlesUsesTheRenewedEvent(int number, string correctTitle, string wrongTitle)
    {
        var stage = DokkanConstants.Stages.First(s => s.Name == "Collection of Epic Battles" && s.StageNumber == number);
        Assert.That(StageTitleCatalog.ForStage(stage).EventId, Is.EqualTo(1769));
        Assert.That(AzureBlobService.ValidateScreenshot(stage, new() {
            EventTitle = stage.Name, StageTitle = correctTitle, Difficulty = "SUPER3"
        }).Outcome, Is.EqualTo("match"));
        Assert.Throws<UploadRejectedException>(() => AzureBlobService.ValidateScreenshot(stage, new() {
            EventTitle = stage.Name, StageTitle = wrongTitle, Difficulty = "SUPER3"
        }));
    }

    [TestCase("Fearsome Activation! Cell Max", "Fearsome Activation! Cell Max", "different stage")]
    [TestCase("Heart-Pounding Heroine Battle", "Vs. Mai", "different event")]
    public async Task RejectsWrongScreenshotBeforeStorageUsingServerAssignment(string eventTitle, string stageTitle, string message)
    {
        var stage = DokkanConstants.Stages.First(s => s.Name == "Fearsome Activation! Cell Max" && s.StageNumber == 2);
        var current = new Challenge(DailyType.Category, stage, null, new Category("Test", Tier.Z), null, null, DateTime.Today);
        var client = new Challenge(DailyType.Category, new("Unconfigured", Tier.Z, "test"), null, current.Category, null, null, current.Date);
        var rng = new Mock<IRngHelperService>();
        rng.Setup(x => x.GetDailyChallenge()).ReturnsAsync(current);
        var admission = new Mock<IUploadAttemptLimiter>();
        admission.Setup(x => x.TryAcceptAsync("123", null)).ReturnsAsync(new UploadAdmission(true, "discord:123", DateOnly.FromDateTime(current.Date)));
        var ocr = new Mock<IOcrService>();
        ocr.Setup(x => x.ProcessImage(It.IsAny<MemoryStream>())).Returns(new ClearMetadata { EventTitle = eventTitle, StageTitle = stageTitle, Difficulty = "SUPER3" });
        var file = new Mock<IBrowserFile>();
        file.Setup(x => x.OpenReadStream(It.IsAny<long>(), It.IsAny<CancellationToken>())).Returns(() => new MemoryStream([1, 2, 3]));
        var service = new AzureBlobService(Options.Create(new DokkanDailySettings { AzureBlobConnectionString = "intentionally invalid", AzureBlobContainerName = "test" }), NullLogger<AzureBlobService>.Instance, ocr.Object, rng.Object, admission.Object);
        var error = Assert.ThrowsAsync<UploadRejectedException>(() => service.UploadToAzureAsync("clear.png", "image/png", file.Object, client, discordId: "123"));
        Assert.That(error.Message, Does.Contain(message));
        ocr.Verify(x => x.ProcessImage(It.IsAny<MemoryStream>()), Times.Once);
        await service.WaitForPendingAnalysis(TimeSpan.FromMilliseconds(50)).WaitAsync(TimeSpan.FromSeconds(5));
    }

    [Test]
    public void FrozenRuntimeRegression()
    {
        using var fixture = JsonDocument.Parse(File.ReadAllText(Path.Combine(Fixtures, "runtime-regression.json")));
        foreach (var row in fixture.RootElement.EnumerateArray())
        {
            string id = row.GetProperty("id").GetString();
            byte[] bytes = File.ReadAllBytes(Path.Combine(Root, row.GetProperty("sourcePath").GetString()));
            Assert.That(Convert.ToHexStringLower(SHA256.HashData(bytes)), Is.EqualTo(row.GetProperty("sourceSha256").GetString()), id);
            var provider = new OcrFormatProvider();
            provider.SetParsingMode(row.GetProperty("inferredLayout").GetString() == "japanese" ? ParsingMode.Japanese : ParsingMode.English);
            var reading = StageTitleReader.Read(bytes, provider);
            var expectedMetadata = row.GetProperty(OperatingSystem.IsLinux() ? "linuxMetadata" : "applicationPrediction").Deserialize<ClearMetadata>(Json);
            Assert.Multiple(() =>
            {
                Assert.That(reading.EventTitle, Is.EqualTo((OperatingSystem.IsLinux() ? expectedMetadata.EventTitle : row.GetProperty("eventTitle").GetString())), id);
                Assert.That(reading.StageTitle, Is.EqualTo((OperatingSystem.IsLinux() ? expectedMetadata.StageTitle : row.GetProperty("stageTitle").GetString())), id);
                Assert.That(reading.CandidateSelected, Is.EqualTo(row.GetProperty("candidateSelected").GetBoolean()), id);
                Assert.That(reading.Fallback, Is.EqualTo(row.GetProperty("geometryStatus").GetString() == "fixed-band-fallback"), id);
                Assert.That(new[] { reading.Bounds.X, reading.Bounds.Y, reading.Bounds.Width, reading.Bounds.Height }, Is.EqualTo(row.GetProperty("bounds").Deserialize<int[]>()), id);
                Assert.That(reading.Lines.Select(r => new[] { r.X, r.Y, r.Width, r.Height }), Is.EqualTo(row.GetProperty("lineCoordinates").Deserialize<int[][]>()), id);
            });
            var service = new OcrService(NullLogger<OcrService>.Instance, Options.Create(new DokkanDailySettings { FeatureFlags = new() { EnableJapaneseParsing = true } }), new());
            using var stream = new MemoryStream(bytes);
            var actual = service.ProcessImage(stream);
            Assert.That(actual is not null, Is.EqualTo(row.GetProperty("applicationAccepted").GetBoolean()), id);
            if (actual is not null)
            {
                Assert.That(actual.EventTitle, Is.EqualTo(reading.EventTitle), id);
                Assert.That(actual.StageTitle, Is.EqualTo(reading.StageTitle), id);
                var expected = expectedMetadata;
                Assert.That((actual.Difficulty, actual.Nickname, actual.ClearTime, actual.ItemlessClear), Is.EqualTo((expected.Difficulty, expected.Nickname, expected.ClearTime, expected.ItemlessClear)), id);
            }
        }
    }
}
