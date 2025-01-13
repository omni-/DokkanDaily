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
            mocks = new(MockBehavior.Strict);
        }

        [Test]
        public void TestRngService()
        {
            var mock = mocks.Create<IDokkanDailyRepository>();
            IRngHelperService rngHelperService = new RngHelperServiceV2(mock.Object, Options.Create<DokkanDailySettings>(new()), mocks.Create<ILogger<RngHelperServiceV2>>().Object);

            mock
                .Setup(x => x.GetChallengeList(It.IsAny<DateTime?>()))
                .Returns(Task.FromResult<IEnumerable<DbChallenge>>([]));

            var seed1 = rngHelperService.GetRawSeed();
            rngHelperService.RollDailySeed();
            var seed2 = rngHelperService.GetRawSeed();

            Assert.That(seed1, Is.Not.EqualTo(seed2));

            List<string> leaders = [];
            List<string> categories = [];
            List<string> linkSkills = [];
            List<string> dailyTypes = [];

            Assert.DoesNotThrow(() =>
            {
                rngHelperService.RollDailySeed();

                rngHelperService.UpdateDailyChallenge();

                rngHelperService.GetDailyChallenge();

                rngHelperService.SetDailySeed(9);

                rngHelperService.Reset();

                Assert.That(rngHelperService.GetRawSeed(), Is.EqualTo(seed1));

                dailyTypes.Add(rngHelperService.GetTodaysDailyType().ToString());
            });
        }

        [Test]
        public void RngServiceDoesNotThrow()
        {
            var repoMock = mocks.Create<IDokkanDailyRepository>();
            IRngHelperService rngHelperService = new RngHelperServiceV2(repoMock.Object, Options.Create(new DokkanDailySettings() { EventRepeatLimitDays = 99999999, StageRepeatLimitDays = 99999999 }), mocks.Create<ILogger<RngHelperServiceV2>>().Object);

            var list = new List<Stage>(DokkanConstants.Stages);
            list.RemoveAll(x => x.Name.Contains("Heroine"));

            repoMock
                .Setup(x => x.GetChallengeList(It.IsAny<DateTime?>()))
                .Returns(Task.FromResult(list.Select(x => new DbChallenge() { Event = x.Name, Stage = x.StageNumber, DailyTypeName = "Category", Category = "Demonic Power", Date = DateTime.UtcNow })));

            Assert.DoesNotThrowAsync(async () =>
            {
                for (int i = 0; i < 10000; i++)
                {
                    await rngHelperService.SetDailySeed(i);
                    var chall = await rngHelperService.GetDailyChallenge();
                    Assert.That(chall.TodaysEvent.Name, Does.Contain("Heroine"));
                }
            });
        }

        [Test]
        public void TestDailyResetService()
        {
            var abMock = mocks.Create<IAzureBlobService>();
            var repoMock = mocks.Create<IDokkanDailyRepository>();
            var lbMock = mocks.Create<ILeaderboardService>();
            var rngMock = mocks.Create<IRngHelperService>();
            var loggerMock = mocks.Create<ILogger<ResetService>>(MockBehavior.Loose);
            var loggerMock2 = mocks.Create<ILogger<DiscordWebhookClient>>(MockBehavior.Loose);
            var httpMock = mocks.Create<HttpClient>(MockBehavior.Loose);
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
            abMock.Setup(x => x.PruneContainers(30)).Returns(Task.CompletedTask);
            abMock.Setup(x => x.GetBucketNameForDate(It.IsAny<string>())).Returns(It.IsAny<string>());

            repoMock
                .Setup(x => x.InsertDailyClears(It.IsAny<List<DbClear>>(), It.IsAny<DateTime>()))
                .Returns(Task.CompletedTask)
                .Callback<IEnumerable<DbClear>, DateTime>((x, y) => actual = x.ToList());

            repoMock.Setup(x => x.InsertChallenge(It.IsAny<Challenge>())).Returns(Task.CompletedTask);

            rngMock
                .Setup(x => x.UpdateDailyChallenge())
                .ReturnsAsync(new Challenge(DailyType.Character, new("foo", Tier.D, "LGE"), new("bar", Tier.D), new("baz", Tier.D), new("quz", "", Tier.D), new()));
            rngMock
                .Setup(x => x.GetDailyChallenge())
                .ReturnsAsync(new Challenge(DailyType.Character, new("foo", Tier.D, "LGE"), new("bar", Tier.D), new("baz", Tier.D), new("quz", "", Tier.D), new()));

            lbMock.Setup(x => x.GetCurrentLeaderboard(It.IsAny<bool>())).Returns(Task.FromResult<List<LeaderboardUser>>([]));

            webhookMock.Setup(x => x.PostAsync(It.IsAny<WebhookMessage>())).Returns(Task.CompletedTask);

            Assert.DoesNotThrowAsync(() => tdrs.DoReset());


            List<DbClear> exp =
            [
                new() { DokkanNickname = "omni", ClearTime = "0'20\"10.8", IsDailyHighscore = false, ItemlessClear = true, ClearTimeSpan = new TimeSpan(0, 0, 20, 10, 800) },
                new() { DokkanNickname = "owl", ClearTime = "0'18\"10.8", IsDailyHighscore = true, ItemlessClear = false, ClearTimeSpan = new TimeSpan(0, 0, 18, 10, 800) },
                new() { DokkanNickname = "rabs", ClearTime = "0'30\"10.8", IsDailyHighscore = false, ItemlessClear = true, ClearTimeSpan = new TimeSpan(0, 0, 30, 10, 800) }
            ];

            actual.Should().BeEquivalentTo(exp);

            rngMock.Verify(x => x.GetDailyChallenge(), Times.Once);
            rngMock.Verify(x => x.UpdateDailyChallenge(), Times.Once);
            rngMock.VerifyNoOtherCalls();

            lbMock.Verify(x => x.GetCurrentLeaderboard(true), Times.Once);
            lbMock.VerifyNoOtherCalls();

            abMock.Verify(x => x.PruneContainers(It.IsAny<int>()), Times.Once);
            abMock.Verify(x => x.GetBucketNameForDate(It.IsAny<string>()));
            abMock.Verify(x => x.GetFilesForTag(It.IsAny<string>(), It.IsAny<string>()), Times.Once);
            abMock.VerifyNoOtherCalls();

            repoMock.Verify(x => x.InsertDailyClears(It.IsAny<IEnumerable<DbClear>>(), It.IsAny<DateTime>()), Times.Once);
            repoMock.Verify(x => x.InsertChallenge(It.IsAny<Challenge>()), Times.Once);
            repoMock.VerifyNoOtherCalls();

            webhookMock.Verify(x => x.PostAsync(It.IsAny<WebhookMessage>()), Times.Once);
        }
    }
}