using DokkanDaily.Constants;
using DokkanDaily.Helpers;
using DokkanDaily.Models;
using DokkanDaily.Models.Database;
using DokkanDaily.Repository;
using DokkanDaily.Services;
using DokkanDailyTests.Infra;
using FluentAssertions;
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
        public async Task DailyResetServiceWorks()
        {
            var abMock = mocks.Create<IAzureBlobService>();
            var repoMock = mocks.Create<IDokkanDailyRepository>();
            var loggerMock = mocks.Create<ILogger<ResetService>>();

            IResetService tdrs = new ResetService(abMock.Object, repoMock.Object, loggerMock.Object);

            List<DbClear> actual = [];

            abMock
                .Setup(x => x.GetFilesForTag(It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync([
                    new MockBlobClient(new Dictionary<string, string>()
                    {
                        { DDConstants.USER_NAME_TAG, "omni" },
                        { DDConstants.CLEAR_TIME_TAG, "0'20\"10.8" },
                        { DDConstants.ITEMLESS_TAG, "true" }
                    }),
                    new MockBlobClient(new Dictionary<string, string>()
                    {
                        { DDConstants.USER_NAME_TAG, "owl" },
                        { DDConstants.CLEAR_TIME_TAG, "0'44\"10.8" },
                        { DDConstants.ITEMLESS_TAG, "false" }
                    }),
                    new MockBlobClient(new Dictionary<string, string>()
                    {
                        { DDConstants.USER_NAME_TAG, "owl" },
                        { DDConstants.CLEAR_TIME_TAG, "0'18\"10.8" },
                        { DDConstants.ITEMLESS_TAG, "false" }
                    }),
                    new MockBlobClient(new Dictionary<string, string>()
                    {
                        { DDConstants.USER_NAME_TAG, "rabs" },
                        { DDConstants.CLEAR_TIME_TAG, "0'30\"10.8" },
                        { DDConstants.ITEMLESS_TAG, "true" }
                    }),
                ]);

            repoMock
                .Setup(x => x.InsertDailyClears(It.IsAny<List<DbClear>>()))
                .Callback<IEnumerable<DbClear>>(x => actual = x.ToList());

            await tdrs.DoReset();

            List<DbClear> exp = 
            [
                new() { DokkanNickname = "omni", ClearTime = "0'20\"10.8", IsDailyHighscore = false, ItemlessClear = true, ClearTimeSpan = new TimeSpan(0, 0, 20, 10, 800) },
                new() { DokkanNickname = "owl", ClearTime = "0'18\"10.8", IsDailyHighscore = true, ItemlessClear = false, ClearTimeSpan = new TimeSpan(0, 0, 18, 10, 800) },
                new() { DokkanNickname = "rabs", ClearTime = "0'30\"10.8", IsDailyHighscore = false, ItemlessClear = true, ClearTimeSpan = new TimeSpan(0, 0, 30, 10, 800) }
            ];
            
            actual.Should().BeEquivalentTo(exp);
        }
    }
}