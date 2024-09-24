using DokkanDaily.Services;

namespace DokkanDailyTests
{
    [TestFixture]
    public class OcrTests
    {
        [Test]
        [Ignore("Github runner cant run this")]
        public void BasicOcrTest()
        {
            OcrService service = new();
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
        }
    }
}
