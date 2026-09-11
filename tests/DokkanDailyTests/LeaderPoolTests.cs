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
            RngService service = CreateService();

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
            RngService service = CreateService();
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
            Mock<ILogger<RngService>> logger = new();
            RngService service = CreateService(logger.Object);

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

        [Test]
        public void TierWeightsRemainTwoToOneAndClosestFallbackDoesNotAlwaysPickFirst()
        {
            var service = CreateService();
            var exact = new Leader("exact", "exact", Tier.A);
            var adjacent = new Leader("adjacent", "adjacent", Tier.B);
            var random = new Random(123);
            int exactCount = Enumerable.Range(0, 6000).Count(_ =>
                ReferenceEquals(service.PickWithFallback(new[] { exact, adjacent }, new[] { exact, adjacent }, random, Tier.A, "test"), exact));
            Assert.That(exactCount, Is.InRange(3800, 4200));
            var other = new Leader("other", "other", Tier.A);
            var picks = Enumerable.Range(0, 100).Select(_ =>
                service.PickWithFallback(Array.Empty<Leader>(), new[] { exact, other }, random, Tier.Z, "test")).Distinct().ToArray();
            Assert.That(picks, Has.Length.EqualTo(2));
        }

        private static RngService CreateService(ILogger<RngService> logger = null)
            => new(
                Mock.Of<IDokkanDailyRepository>(),
                Options.Create(new DokkanDailySettings()),
                logger ?? Mock.Of<ILogger<RngService>>());
    }
}
