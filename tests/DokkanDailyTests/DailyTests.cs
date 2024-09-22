using Azure.Storage.Blobs;
using DokkanDaily.Constants;
using DokkanDaily.Helpers;
using DokkanDaily.Models;
using DokkanDaily.Repository;
using DokkanDaily.Services;
using DokkanDailyTests.Infra;
using Microsoft.Extensions.Logging;
using Moq;

namespace DokkanDailyTests
{
    [TestFixture]
    public class DailyTests
    {
        MockRepository mocks;

        [SetUp]
        public void Setup()
        {
            mocks = new(MockBehavior.Loose);
        }

        [Test]
        public void CanBuildCharacterDb()
        {
            IEnumerable<Unit> list = [];

            Assert.DoesNotThrow(() => list = DDHelper.BuildCharacterDb());
            Assert.That(list, Is.Not.Null);
            Assert.That(list, Is.Not.Empty);
        }

        [Test]
        public void TestHelpers()
        {
            Assert.DoesNotThrow(() => DDConstants.GetUnit("Tenacious Secret Plan", "Super Vegeta"));
        }

        [Test]
        public void DailyResetServiceWorks()
        {
            // TODO

            //var abMock = mocks.Create<IAzureBlobService>();
            //var repoMock = mocks.Create<IDokkanDailyRepository>();
            //var loggerMock = mocks.Create<ILogger<TestDailyResetService>>();

            //TestDailyResetService tdrs = new(abMock.Object, repoMock.Object, loggerMock.Object);

            //abMock.Setup(x => x.GetFilesForTag(It.IsAny<string>(), It.IsAny<string>())).ReturnsAsync(
            //    [
            //    ]);
        }
    }
}