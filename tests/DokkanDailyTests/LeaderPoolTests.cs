using DokkanDaily.Configuration;
using DokkanDaily.Models;
using DokkanDaily.Models.Enums;
using DokkanDaily.Repository;
using DokkanDaily.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;

namespace DokkanDailyTests
{
    [TestFixture]
    public class LeaderPoolTests
    {
        [Test]
        public void BasePoolExcludesLeaderWithoutMatchingUnit()
        {
            Leader matched = new("Matched title", "Matched name", Tier.A);
            Leader unmatched = new("Missing title", "Missing name", Tier.A);
            RngHelperServiceV2 service = CreateService();

            IReadOnlyList<Leader> pool = service.BuildEligibleLeaderBasePool(
                [matched, unmatched],
                [new Unit { Title = matched.Title, Name = matched.Name }]);

            Assert.That(pool, Is.EqualTo(new[] { matched }));
        }

        [Test]
        public void ExhaustedFilteredLeaderPoolFallsBackOnlyToEligibleBasePool()
        {
            Leader matched = new("Matched title", "Matched name", Tier.A);
            Leader unmatched = new("Missing title", "Missing name", Tier.A);
            RngHelperServiceV2 service = CreateService();
            IReadOnlyList<Leader> basePool = service.BuildEligibleLeaderBasePool(
                [matched, unmatched],
                [new Unit { Title = matched.Title, Name = matched.Name }]);

            Leader selected = service.PickWithFallback(
                Array.Empty<Leader>(), basePool, new Random(1), Tier.A, "leaders");

            Assert.That(selected, Is.SameAs(matched));
        }

        [Test]
        public void EmptyEligibleBasePoolFailsWithClearError()
        {
            Mock<ILogger<RngHelperServiceV2>> logger = new();
            RngHelperServiceV2 service = CreateService(logger.Object);

            Action build = () => service.BuildEligibleLeaderBasePool(
                [new Leader("Missing title", "Missing name", Tier.A)],
                Array.Empty<Unit>());

            Assert.That(build, Throws.InvalidOperationException.With.Message.Contains("no configured leader has a matching unit"));
            logger.Verify(x => x.Log(
                LogLevel.Critical,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((value, _) => value.ToString().Contains("no configured leader has a matching unit")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception, string>>()), Times.Once);
        }

        private static RngHelperServiceV2 CreateService(ILogger<RngHelperServiceV2> logger = null)
            => new(
                Mock.Of<IDokkanDailyRepository>(),
                Options.Create(new DokkanDailySettings()),
                logger ?? Mock.Of<ILogger<RngHelperServiceV2>>());
    }
}
