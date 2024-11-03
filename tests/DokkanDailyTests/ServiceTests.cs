using DokkanDaily.Configuration;
using DokkanDaily.Constants;
using DokkanDaily.Models;
using DokkanDaily.Models.Database;
using DokkanDaily.Models.Enums;
using DokkanDaily.Repository;
using DokkanDaily.Services;
using DokkanDaily.Services.Interfaces;
using DokkanDailyTests.Infra;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;

namespace DokkanDailyTests
{
    [TestFixture]
    public class ServiceTests
    {
        MockRepository mocks;

        [SetUp]
        public void Setup()
        {
            mocks = new(MockBehavior.Loose);
        }

        [Test]
        public void TestRngService()
        {
            IRngHelperService rngHelperService = new RngHelperService();

            var seed1 = rngHelperService.GetRawSeed();
            rngHelperService.RollDailySeed();
            var seed2 = rngHelperService.GetRawSeed();

            Assert.That(seed1, Is.Not.EqualTo(seed2));

            List<string> leaders = [];
            List<string> categories = [];
            List<string> linkSkills = [];
            List<string> dailyTypes = [];

            Tier t = Tier.S;

            Assert.DoesNotThrow(() =>
            {
                rngHelperService = new RngHelperService();

                rngHelperService.RollDailySeed();

                rngHelperService.GetRawSeed();

                rngHelperService.GetTomorrowsChallenge();
                rngHelperService.GetDailyChallenge();

                rngHelperService.SetDailySeed(9);

                rngHelperService.Reset();

                leaders.Add(rngHelperService.GetRandomLeader(t).Name);
                categories.Add(rngHelperService.GetRandomCategory(t).Name);
                linkSkills.Add(rngHelperService.GetRandomLinkSkill(t).Name);

                rngHelperService.GetRandomStage();
                dailyTypes.Add(rngHelperService.GetRandomDailyType().ToString());
            });
        }

        [Test]
        public void TestDailyResetService()
        {
            var abMock = mocks.Create<IAzureBlobService>();
            var repoMock = mocks.Create<IDokkanDailyRepository>();
            var lbMock = mocks.Create<ILeaderboardService>();
            var rngMock = mocks.Create<IRngHelperService>();
            var loggerMock = mocks.Create<ILogger<ResetService>>();
            var loggerMock2 = mocks.Create<ILogger<DiscordWebhookClient>>();
            var httpMock = mocks.Create<HttpClient>();
            var webhookMock = mocks.Create<DiscordWebhookClient>(loggerMock2.Object, httpMock.Object, Options.Create<DokkanDailySettings>(new() { WebhookUrl = "http://foo.bar" }));

            IResetService tdrs = new ResetService(abMock.Object, repoMock.Object, lbMock.Object, rngMock.Object, webhookMock.Object, loggerMock.Object);

            List<DbClear> actual = [];

            abMock
                .Setup(x => x.GetFilesForTag(It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync([
                    new MockBlobClient(new Dictionary<string, string>()
                    {
                        { AzureConstants.USER_NAME_TAG, "omni" },
                        { AzureConstants.CLEAR_TIME_TAG, "0'20\"10.8" },
                        { AzureConstants.ITEMLESS_TAG, "true" }
                    }),
                    new MockBlobClient(new Dictionary<string, string>()
                    {
                        { AzureConstants.USER_NAME_TAG, "omni" },
                        { AzureConstants.CLEAR_TIME_TAG, "0'19\"10.8" },
                        { AzureConstants.ITEMLESS_TAG, "false" }
                    }),
                    new MockBlobClient(new Dictionary<string, string>()
                    {
                        { AzureConstants.USER_NAME_TAG, "owl" },
                        { AzureConstants.CLEAR_TIME_TAG, "0'44\"10.8" },
                        { AzureConstants.ITEMLESS_TAG, "false" }
                    }),
                    new MockBlobClient(new Dictionary<string, string>()
                    {
                        { AzureConstants.USER_NAME_TAG, "owl" },
                        { AzureConstants.CLEAR_TIME_TAG, "0'18\"10.8" },
                        { AzureConstants.ITEMLESS_TAG, "false" }
                    }),
                    new MockBlobClient(new Dictionary<string, string>()
                    {
                        { AzureConstants.USER_NAME_TAG, "owl" },
                        { AzureConstants.CLEAR_TIME_TAG, "0'48\"10.8" },
                        { AzureConstants.ITEMLESS_TAG, "true" }
                    }),
                    new MockBlobClient(new Dictionary<string, string>()
                    {
                        { AzureConstants.USER_NAME_TAG, "rabs" },
                        { AzureConstants.CLEAR_TIME_TAG, "0'30\"10.8" },
                        { AzureConstants.ITEMLESS_TAG, "true" }
                    }),
                ]);

            repoMock
                .Setup(x => x.InsertDailyClears(It.IsAny<List<DbClear>>(), It.IsAny<DateTime>()))
                .Callback<IEnumerable<DbClear>, DateTime>((x, y) => actual = x.ToList());

            rngMock
                .Setup(x => x.GetTomorrowsChallenge())
                .Returns(new Challenge(DailyType.Character, new("foo", Tier.D, "LGE"), new("bar", Tier.D), new("baz", Tier.D), new("quz", "", Tier.D), new()));

            Assert.DoesNotThrowAsync(() => tdrs.DoReset());


            List <DbClear> exp =
            [
                new() { DokkanNickname = "omni", ClearTime = "0'20\"10.8", IsDailyHighscore = false, ItemlessClear = true, ClearTimeSpan = new TimeSpan(0, 0, 20, 10, 800) },
                new() { DokkanNickname = "owl", ClearTime = "0'18\"10.8", IsDailyHighscore = true, ItemlessClear = false, ClearTimeSpan = new TimeSpan(0, 0, 18, 10, 800) },
                new() { DokkanNickname = "rabs", ClearTime = "0'30\"10.8", IsDailyHighscore = false, ItemlessClear = true, ClearTimeSpan = new TimeSpan(0, 0, 30, 10, 800) }
            ];

            actual.Should().BeEquivalentTo(exp);

            lbMock.Verify(x => x.GetDailyLeaderboard(true), Times.Once());
            lbMock.VerifyNoOtherCalls();

            abMock.Verify(x => x.PruneContainers(It.IsAny<int>()), Times.Once());
            abMock.Verify(x => x.GetBucketNameForDate(It.IsAny<string>()));
            abMock.Verify(x => x.GetFilesForTag(It.IsAny<string>(), It.IsAny<string>()), Times.Once());
            abMock.VerifyNoOtherCalls();

            repoMock.Verify(x => x.InsertDailyClears(It.IsAny<IEnumerable<DbClear>>(), It.IsAny<DateTime>()), Times.Once());
            repoMock.VerifyNoOtherCalls();
        }
    }
}