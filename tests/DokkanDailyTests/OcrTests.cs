using DokkanDaily.Services;
using System.Diagnostics;

namespace DokkanDailyTests
{
    [TestFixture]
    public class OcrTests
    {
        private TextWriterTraceListener _traceListener;

        [SetUp]
        public void SetUp()
        {
            // Create a listener that writes to the TestContext output (captured by NUnit)
            _traceListener = new TextWriterTraceListener(TestContext.Out);

            // Add the listener to the global Trace.Listeners collection
            Trace.Listeners.Add(_traceListener);

            // Optionally, you can set the default level of tracing to capture all events
            Trace.AutoFlush = true;
        }

        [TearDown]
        public void TearDown()
        {
            // Remove the listener after each test
            Trace.Listeners.Remove(_traceListener);

            // Clean up the listener after the test to avoid any resource leaks
            if (_traceListener != null)
            {
                _traceListener.Flush();
                _traceListener.Close();
            }
        }

        [Test]
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
