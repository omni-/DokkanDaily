using DokkanDaily.Constants;
using DokkanDaily.Helpers;
using DokkanDaily.Models;
using DokkanDaily.Models.Enums;
using DokkanDaily.Services;

namespace DokkanDailyTests
{
    [TestFixture]
    public class HelperTests
    {
        [TestCase("valid", "match", null, true)]
        [TestCase("valid", "match", "false", true)]
        [TestCase("valid", "match", "true", false)]
        [TestCase("valid", "match", "malformed", false)]
        [TestCase("valid", "unknown", null, true)]
        [TestCase("valid", "stage-mismatch", null, false)]
        [TestCase("valid", null, null, true)]
        [TestCase("unknown", "match", null, false)]
        [TestCase("pending", "match", null, false)]
        [TestCase("invalid", "match", null, false)]
        [TestCase(null, "match", null, true)]
        [TestCase(null, null, null, true)]
        public void GalleryRequiresRecognizedClearDataRegardlessOfTitleReadability(string status, string validation, string invalid, bool visible)
        {
            Dictionary<string, string> metadata = new()
            {
                [AzureConstants.USER_NAME_TAG] = "Tester",
                [AzureConstants.CLEAR_TIME_TAG] = "0'01\"00.0",
                [AzureConstants.ITEMLESS_TAG] = "true"
            };
            if (status is not null) metadata[AzureConstants.UPLOAD_STATUS_TAG] = status;
            if (validation is not null) metadata[AzureConstants.STAGE_VALIDATION_TAG] = validation;
            if (invalid is not null) metadata[AzureConstants.INVALID_TAG] = invalid;
            Assert.That(DokkanDailyHelper.IsConfirmedClear(metadata), Is.EqualTo(visible));
        }

        [TestCase("username")]
        [TestCase("cleartime")]
        [TestCase("itemless")]
        public void GalleryHidesUploadsMissingCoreClearData(string missingField)
        {
            Dictionary<string, string> metadata = new()
            {
                [AzureConstants.UPLOAD_STATUS_TAG] = "valid",
                [AzureConstants.STAGE_VALIDATION_TAG] = "match",
                [AzureConstants.USER_NAME_TAG] = "Tester",
                [AzureConstants.CLEAR_TIME_TAG] = "0'01\"00.0",
                [AzureConstants.ITEMLESS_TAG] = "false"
            };
            Assert.That(DokkanDailyHelper.IsConfirmedClear(metadata), Is.True);
            metadata.Remove(missingField);
            Assert.That(DokkanDailyHelper.IsConfirmedClear(metadata), Is.False);
        }

        [Test]
        public void IncidentMetadataDoesNotDisplayWithoutRecognizedClearData()
        {
            Dictionary<string, string> metadata = new()
            {
                [AzureConstants.EVENT_TAG] = "When Wishes Backfire! Birth of the Shadow Dragons, Stage 9",
                [AzureConstants.DAILY_TYPE_TAG] = "LinkSkill",
                [AzureConstants.UPLOAD_STATUS_TAG] = "valid",
                [AzureConstants.STAGE_VALIDATION_TAG] = "unknown",
                [AzureConstants.STAGE_VALIDATION_REASON_TAG] = "missing-or-unsupported-difficulty"
            };
            Assert.That(DokkanDailyHelper.IsConfirmedClear(metadata), Is.False);
            Assert.That(DokkanDailyHelper.IsConfirmedClear(null), Is.False);
        }

        [Test]
        public void EveryCurrentStageHasAValidDokkanInfoDestination()
        {
            foreach (var stage in DokkanConstants.Stages)
            {
                Assert.That(stage.DokkanInfoUrl, Does.Match(@"^https://dokkaninfo\.com/events/challenge/[0-9]+(?:/[0-9]+)?$"), stage.FullName);
            }
            Assert.That(new Stage("Unknown future event", Tier.S, "unknown").DokkanInfoUrl, Is.Null);
        }

        [Test]
        [TestCase("cats.png", null, @"^cats-[0-9a-f]{32}\.png$")]
        [TestCase("../../escape.png", null, @"^escape-[0-9a-f]{32}\.png$")]
        [TestCase(@"..\..\windows\c.png", null, @"^c-[0-9a-f]{32}\.png$")]
        [TestCase("cats.png", "112089455933792256", @"^112089455933792256/cats-[0-9a-f]{32}\.png$")]
        public void BlobNamesAreSanitizedUniqueAndScopedToTheUploader(string file, string discordId, string expected)
        {
            string output = DokkanDailyHelper.BuildBlobName(file, discordId);

            Assert.That(output, Does.Match(expected));
        }

        [Test]
        public void BlobNamesNeverEscapeTheOwnerDirectory()
        {
            string output = DokkanDailyHelper.BuildBlobName("../../../other-user/steal.png", "42");

            Assert.That(output, Does.StartWith("42/"));
            Assert.That(output, Does.Not.Contain(".."));
        }

        [Test]
        public void BlobNamesAreUniquePerUpload()
        {
            string first = DokkanDailyHelper.BuildBlobName("clear.png", "42");
            string second = DokkanDailyHelper.BuildBlobName("clear.png", "42");

            Assert.That(first, Is.Not.EqualTo(second));
        }

        [Test]
        public void InitialBlobMetadataIsAsciiSafe()
        {
            Challenge challenge = new(
                DailyType.Character,
                new Stage("Event", Tier.S, "event"),
                null,
                null,
                new Leader("Power of Rosé", "Gokū Black", Tier.S),
                null,
                DateTime.UtcNow);

            Dictionary<string, string> metadata = AzureBlobService.BuildIdentityTagDict(
                challenge,
                "Coopér",
                "42",
                "127.0.0.1",
                "Bröwser");

            Assert.That(metadata.Values.All(value => value.All(character => character <= 127)), Is.True);
            Assert.That(metadata[AzureConstants.CHALLENGE_TARGET_TAG], Does.Contain(@"\u00e9"));
            Assert.That(metadata[AzureConstants.UPLOAD_STATUS_TAG], Is.EqualTo(AzureConstants.UPLOAD_STATUS_PENDING));
        }


        [Test]
        public void CanBuildCharacterDb()
        {
            IEnumerable<Unit> list = [];

            Assert.DoesNotThrow(() => list = DokkanDailyHelper.BuildCharacterDb());
            Assert.That(list, Is.Not.Null);
            Assert.That(list, Is.Not.Empty);
        }

        [Test]
        public void TestGetUnit()
        {
            foreach (Leader l in DokkanConstants.Leaders)
            {
                Unit u = null;
                Assert.DoesNotThrow(() => u = DokkanDailyHelper.GetUnit(l), $"Error getting {l.FullName}");
                Assert.That(u, Is.Not.Null, $"Didn't find expected value {l.FullName}");
                Assert.That(File.Exists(Path.Combine("wwwroot", u.ImageUrl)), $"Could not find thumb art for {l.FullName} on disk");
            }
        }

        [Test]
        public void TestCategories()
        {
            foreach(Category c in DokkanConstants.Categories)
            {
                string path = Path.Combine("wwwroot", c.ImageURL);
                Assert.That(File.Exists(path), Is.True, $"Path {path} not found on disk");
            }
        }

        [Test]
        public void TestEvents()
        {
            foreach(Stage stage in DokkanConstants.Stages)
            {
                Assert.That(File.Exists(Path.Combine("wwwroot", stage.WallpaperImagePath)), Is.True, $"Path {stage.WallpaperImagePath} not found on disk");
                Assert.That(File.Exists(Path.Combine("wwwroot", stage.BannerImagePath)), Is.True, $"Path {stage.BannerImagePath} not found on disk");
            }
        }

        [Test]
        public void TestParseTime()
        {
            var str = "0'09\"12.3";
            var exp = new TimeSpan(0, 0, 9, 12, 300);

            var success = DokkanDailyHelper.TryParseDokkanTimeSpan(str, out TimeSpan result);

            Assert.That(success, Is.True);
            Assert.That(result, Is.EqualTo(exp));
        }

        [Test]
        public void TestEscapeUnicode()
        {
            string unescaped = DokkanDailyHelper.UnescapeUnicode(@"\u4E94\u6761\u609F");
            string escaped = DokkanDailyHelper.EscapeUnicode("五条悟");

            Assert.That(unescaped, Is.EqualTo("五条悟"));
            Assert.That(escaped, Is.EqualTo(@"\u4e94\u6761\u609f"));
        }

        [Test]
        [TestCase("UBCeomnt", "DBC*omni")]
        [TestCase("OBC*FIGO", "DBC*FIGO")]
        [TestCase("DBC +FIGO", "DBC*FIGO")]
        [TestCase("OBC*FIGO", "DBC*FIGO")]
        [TestCase("五新悟", "五条悟")]
        [TestCase("DBC *Owl", "DBC*Owl")]
        [TestCase("DBC* Owl", "DBC*Owl")]
        [TestCase("DBC * Owl", "DBC*Owl")]
        [TestCase("DBCeomni", "DBCeomni")]
        [TestCase("DBC*Brood", "DBC*Brood")]
        [TestCase("ExoticDJ85", "ExoticDJ85")]
        public void TestCheckUsername(string username, string exp)
        {
            Assert.That(DokkanDailyHelper.FixUsername(username), Is.EqualTo(exp));
        }

        [Test]
        public void TestUsernameFormatter()
        {
            LeaderboardUser user = new()
            {
                DiscordId = "7777777777777777",
                DokkanNickname = "DBC*omni",
                DiscordUsername = "omni"
            };

            Assert.That(user.GetDisplayName(), Is.EqualTo("omni (DBC*omni)"));
            Assert.That(user.GetDisplayName(true), Is.EqualTo("<@7777777777777777> (DBC*omni)"));

            user = new()
            {
                DiscordId = null,
                DokkanNickname = "DBC*omni",
                DiscordUsername = "omni"
            };

            Assert.That(user.GetDisplayName(), Is.EqualTo("omni (DBC*omni)"));
            Assert.That(user.GetDisplayName(true), Is.EqualTo("omni (DBC*omni)"));
        }

        [Test]
        [TestCase("23:59:45", true)]
        [TestCase("23:59", true)]
        [TestCase("23:59:59.9999999", true)]
        [TestCase("00:00", false)]
        [TestCase("4:59", false)]
        public void TestIsDuringReset(string time, bool result)
        {
            var resetTime = new TimeOnly(23, 59);

            Assert.That(resetTime.IsDuringReset(TimeOnly.Parse(time)), Is.EqualTo(result));
        }
    }
}
