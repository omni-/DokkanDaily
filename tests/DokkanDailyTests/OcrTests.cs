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
        // TODO: Test 9/28 clears to see what went wrong
        public void BasicOcrTest()
        {
            Mock<ILogger<OcrService>> loggerMock = new();
            OcrService service = new(loggerMock.Object);
            MemoryStream ms = new();
            File.OpenRead("Data/IMG_1907.png").CopyTo(ms);
            var result = service.ProcessImage(ms);
            Assert.Multiple(() =>
            {
                Assert.That(result.ItemlessClear, Is.True);
                Assert.That(result.Nickname, Is.EqualTo("DBC*omni"));
                Assert.That(result.ClearTime, Is.EqualTo("0'11\"53.4"));
            });

            ms.Close();
            ms = new();
            File.OpenRead("Data/Screenshot_20240915-192306.png").CopyTo(ms);
            result = service.ProcessImage(ms);
            Assert.Multiple(() =>
            {
                Assert.That(result.ItemlessClear, Is.False);
                Assert.That(result.Nickname, Is.EqualTo("DBC*Owl"));
                Assert.That(result.ClearTime, Is.EqualTo("0'10\"01.8"));
            });

            ms.Close();
            ms = new();
            File.OpenRead("Data/Screenshot_20240925_085515_Dokkan.jpg").CopyTo(ms);
            result = service.ProcessImage(ms);
            Assert.Multiple(() =>
            {
                Assert.That(result.ItemlessClear, Is.False);
                Assert.That(result.Nickname, Is.EqualTo("SlacksV2"));
                Assert.That(result.ClearTime, Is.EqualTo("0'07\"17.1"));
            });
        }
    }
}
