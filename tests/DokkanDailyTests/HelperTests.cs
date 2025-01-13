using DokkanDaily.Constants;
using DokkanDaily.Helpers;
using DokkanDaily.Models;

namespace DokkanDailyTests
{
    [TestFixture]
    public class HelperTests
    {
        [Test]
        [TestCase("aliens.png", "/Agent 47/", "aliens-Agent47.png")]
        [TestCase("IMG_1907.png", "Mozilla/5.0 (Windows NT 10.0; Win64; x64; rv:130.0) Gecko/20100101 Firefox/130.0", "IMG_1907-Mozilla50WindowsNT100Win64x64rv1300Gecko20100101Firefox1300.png")]
        [TestCase("myFile.LongName.WithDots.pdf", "idk/1.0 how; a; user agent; works", "myFile.LongName.WithDots-idk10howauseragentworks.pdf")]
        [TestCase("cats.jpg", "", "cats.jpg")]
        public void CanAddAgentToFileName(string file, string agent, string result)
        {
            string output = DokkanDailyHelper.AddUserAgentToFileName(file, agent);

            Assert.That(output, Is.EqualTo(result));
        }

        [Test]
        public void NullAgentWorks()
        {
            Assert.That(DokkanDailyHelper.AddUserAgentToFileName("cats.png", null), Is.EqualTo("cats.png"));
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
                Assert.DoesNotThrow(() => DokkanDailyHelper.GetUnit(l));
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
            Assert.That(DokkanDailyHelper.CheckUsername(username), Is.EqualTo(exp));
        }
    }
}
