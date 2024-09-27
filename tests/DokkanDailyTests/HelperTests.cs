using DokkanDaily.Helpers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
            string output = DDHelper.AddUserAgentToFileName(file, agent);

            Assert.That(output, Is.EqualTo(result));
        }

        [Test]
        public void NullAgentWorks()
        {
            Assert.That(DDHelper.AddUserAgentToFileName("cats.png", null), Is.EqualTo("cats.png"));
        }
    }
}
