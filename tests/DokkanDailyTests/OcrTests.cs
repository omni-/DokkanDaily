using DokkanDaily.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;

namespace DokkanDailyTests
{
    [TestFixture]
    public class OcrTests
    {
        [OneTimeSetUp]
        public void Setup()
        {
            if (Environment.GetEnvironmentVariable("CI") == "true")
            {
                Assert.Ignore("Skipping OCR tests as they currently can't run on remote");
            }
        }

        [Test]
        [TestCase("IMG_1907.png", "DBC*omni", "0'11\"53.4", true)]
        [TestCase("Screenshot_20240915-192306.png", "DBC*Owl", "0'10\"01.8", false)]
        [TestCase("Screenshot_20240925_085515_Dokkan.jpg", "SlacksV2", "0'07\"17.1", false)]
        [TestCase("Screenshot_20240929-002932-Mozilla50LinuxAndroid10KAppleWebKit53736KHTMLlikeGeckoChrome129000MobileSafari53736.png", "DBC*Owl", "0'10\"05.1", false)]
        [TestCase("4ku.png", null, null, null)]
        public void BasicOcrTest(string filename, string nickname, string cleartime, bool? itemless)
        {
            Mock<ILogger<OcrService>> loggerMock = new();
            OcrService service = new(loggerMock.Object, Options.Create(new DokkanDaily.Configuration.DokkanDailySettings()), new OcrFormatProvider());
            MemoryStream ms = new();
            File.OpenRead(Path.Join("Data", filename)).CopyTo(ms);
            var result = service.ProcessImage(ms);
            Assert.That(result?.ItemlessClear, Is.EqualTo(itemless));
            Assert.That(result?.Nickname, Is.EqualTo(nickname));
            Assert.That(result?.ClearTime, Is.EqualTo(cleartime));
        }
    }
}
