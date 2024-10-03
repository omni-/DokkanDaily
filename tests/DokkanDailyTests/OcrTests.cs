using DokkanDaily.Services;
using Microsoft.Extensions.Logging;
using Moq;

namespace DokkanDailyTests
{
    [TestFixture]
    public class OcrTests
    {
        [Test]
        [Ignore("Can't run on remote")]
        [TestCase("IMG_1907.png", "DBC*omni", "0'11\"53.4", true)]
        [TestCase("Screenshot_20240915-192306.png", "DBC*Owl", "0'10\"01.8", false)]
        [TestCase("Screenshot_20240925_085515_Dokkan.jpg", "SlacksV2", "0'07\"17.1", false)]
        [TestCase("Screenshot_20240929-002932-Mozilla50LinuxAndroid10KAppleWebKit53736KHTMLlikeGeckoChrome129000MobileSafari53736.png", "DBC*Owl", "0'10\"05.1", false)]
        public void BasicOcrTest(string filename, string nickname, string cleartime, bool itemless)
        {
            Mock<ILogger<OcrService>> loggerMock = new();
            OcrService service = new(loggerMock.Object);
            MemoryStream ms = new();
            File.OpenRead(Path.Join("Data", filename)).CopyTo(ms);
            var result = service.ProcessImage(ms);
            Assert.That(result.ItemlessClear, Is.EqualTo(itemless));
            Assert.That(result.Nickname, Is.EqualTo(nickname));
            Assert.That(result.ClearTime, Is.EqualTo(cleartime));
        }
    }
}
