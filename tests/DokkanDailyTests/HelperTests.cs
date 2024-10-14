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
            Assert.DoesNotThrow(() => DokkanDailyHelper.GetUnit("Tenacious Secret Plan", "Super Vegeta"));
        }

    }
}
