using DokkanDaily.Models.Database;
using DokkanDaily.Repository;
using DokkanDaily.Services;
using Moq;

namespace DokkanDailyTests
{
    [TestFixture]
    public class LeaderboardServiceTests
    {
        [Test]
        public async Task GetLeaderboardBySeason_RetriesAfterFailedNonforcedFetch()
        {
            var repository = new Mock<IDokkanDailyRepository>(MockBehavior.Strict);
            repository
                .SetupSequence(x => x.GetLeaderboardByDate(It.IsAny<DateTime>()))
                .Returns(FailAsynchronously)
                .ReturnsAsync([
                    new DbLeaderboardResult
                    {
                        DiscordUsername = "player",
                        DokkanNickname = "Player",
                        DiscordId = "123",
                        TotalClears = 2,
                        ItemlessClears = 1,
                        DailyHighscores = 3
                    }
                ]);

            var service = new LeaderboardService(repository.Object);

            Assert.That(
                async () => await service.GetLeaderboardBySeason(1),
                Throws.TypeOf<InvalidOperationException>());

            var result = await service.GetLeaderboardBySeason(1);

            Assert.That(result, Has.Count.EqualTo(1));
            Assert.That(result[0].DiscordId, Is.EqualTo("123"));
            Assert.That(result[0].TotalScore, Is.EqualTo(6));
            repository.Verify(
                x => x.GetLeaderboardByDate(It.IsAny<DateTime>()),
                Times.Exactly(2));
        }

        [Test]
        public async Task GetLeaderboardBySeason_RefetchesAfterSynchronouslyCompletedEmptyResult()
        {
            var repository = new Mock<IDokkanDailyRepository>(MockBehavior.Strict);
            repository
                .SetupSequence(x => x.GetLeaderboardByDate(It.IsAny<DateTime>()))
                .Returns(Task.FromResult<IEnumerable<DbLeaderboardResult>>([]))
                .ReturnsAsync([
                    new DbLeaderboardResult
                    {
                        DiscordUsername = "player",
                        DokkanNickname = "Player",
                        DiscordId = "123"
                    }
                ]);

            var service = new LeaderboardService(repository.Object);

            var emptyResult = await service.GetLeaderboardBySeason(1);
            var retriedResult = await service.GetLeaderboardBySeason(1);

            Assert.That(emptyResult, Is.Empty);
            Assert.That(retriedResult, Has.Count.EqualTo(1));
            repository.Verify(
                x => x.GetLeaderboardByDate(It.IsAny<DateTime>()),
                Times.Exactly(2));
        }

        private static async Task<IEnumerable<DbLeaderboardResult>> FailAsynchronously()
        {
            await Task.Yield();
            throw new InvalidOperationException("temporary failure");
        }
    }
}
