using DokkanDaily.Constants;
using DokkanDaily.Helpers;
using DokkanDaily.Models;
using DokkanDaily.Services;

namespace DokkanDailyTests
{
    [TestFixture]
    public class HelperTests
    {
        [Test]
        [TestCase("cats.png", null, @"^cats-[0-9a-f]{32}\.png$")]
        [TestCase("../../escape.png", null, @"^escape-[0-9a-f]{32}\.png$")]
        // path components are discarded rather than flattened into the name
        [TestCase("a/b/c.png", null, @"^c-[0-9a-f]{32}\.png$")]
        [TestCase(@"..\..\windows\c.png", null, @"^c-[0-9a-f]{32}\.png$")]
        // a name that is nothing but dots falls back to the default, keeping its extension
        [TestCase("....png", null, @"^clear-[0-9a-f]{32}\.png$")]
        [TestCase("", null, @"^clear-[0-9a-f]{32}$")]
        [TestCase("myFile.LongName.WithDots.jpg", null, @"^myFile\.LongName\.WithDots-[0-9a-f]{32}\.jpg$")]
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
            // the user agent used to make the name stable per device, which meant a re-upload
            // silently replaced the earlier blob - uniqueness is now explicit
            string first = DokkanDailyHelper.BuildBlobName("clear.png", "42");
            string second = DokkanDailyHelper.BuildBlobName("clear.png", "42");

            Assert.That(first, Is.Not.EqualTo(second));
        }

        [Test]
        public void BlobNamesDoNotGrowOnRepeatedUpload()
        {
            // feeding a generated name back in - as happens when a user re-uploads a clear they
            // previously downloaded from the site - must not stack suffixes
            string first = DokkanDailyHelper.BuildBlobName("Screenshot_20250317.jpg", null);
            string second = DokkanDailyHelper.BuildBlobName(first, null);
            string third = DokkanDailyHelper.BuildBlobName(second, null);

            Assert.That(third.Length, Is.LessThanOrEqualTo(second.Length + 33));
            Assert.That(third, Does.EndWith(".jpg"));
        }

        [Test]
        [TestCase(null, null)]
        [TestCase("", null)]
        [TestCase("   ", null)]
        [TestCase("112089455933792256", "112089455933792256/")]
        public void UserBlobPrefixIsOnlySetForLoggedInUploads(string discordId, string expected)
        {
            Assert.That(DokkanDailyHelper.GetUserBlobPrefix(discordId), Is.EqualTo(expected));
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
