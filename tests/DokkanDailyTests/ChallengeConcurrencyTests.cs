using DokkanDaily.Configuration;
using DokkanDaily.Constants;
using DokkanDaily.Models.Database;
using DokkanDaily.Models.Enums;
using DokkanDaily.Repository;
using DokkanDaily.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

namespace DokkanDailyTests;

public class ChallengeConcurrencyTests
{
    private sealed class Clock : TimeProvider
    {
        public DateTimeOffset Now = new(2026, 9, 10, 12, 0, 0, TimeSpan.Zero);
        public override DateTimeOffset GetUtcNow() => Now;
    }
    private static RngHelperServiceV2 Create(Mock<IDokkanDailyRepository> repo, TimeProvider clock = null)
        => new(repo.Object, Options.Create(new DokkanDailySettings()), NullLogger<RngHelperServiceV2>.Instance, clock);

    [Test]
    public async Task ConcurrentCacheMissesGenerateOnceAndQueuedOverrideWins()
    {
        var history = new TaskCompletionSource<IEnumerable<DbChallenge>>(TaskCreationOptions.RunContinuationsAsynchronously);
        var repo = new Mock<IDokkanDailyRepository>();
        repo.Setup(x => x.GetChallengeList(null)).Returns(history.Task);
        var service = Create(repo);
        var callers = Enumerable.Range(0, 30).Select(_ => service.GetDailyChallenge()).ToArray();
        history.SetResult([]);
        var challenges = await Task.WhenAll(callers);
        repo.Verify(x => x.GetChallengeList(null), Times.Once);
        Assert.That(challenges.All(x => ReferenceEquals(x, challenges[0])), Is.True);

        var nextHistory = new TaskCompletionSource<IEnumerable<DbChallenge>>(TaskCreationOptions.RunContinuationsAsynchronously);
        repo.Setup(x => x.GetChallengeList(null)).Returns(nextHistory.Task);
        var generation = service.RollDailySeed();
        var category = DokkanConstants.Categories[0];
        var replacement = service.OverrideChallenge(DailyType.Category, DokkanConstants.Stages[0], null, category, null);
        Assert.That(replacement.IsCompleted, Is.False);
        nextHistory.SetResult([]);
        await generation;
        await replacement;
        Assert.That((await service.GetDailyChallenge()).Category, Is.SameAs(category));
    }

    [Test]
    public async Task ExpiryRefreshesSeedAtMidnightAndTypeOverrideDoesNotMutateSnapshot()
    {
        var repo = new Mock<IDokkanDailyRepository>();
        repo.Setup(x => x.GetChallengeList(null)).ReturnsAsync(Array.Empty<DbChallenge>());
        var clock = new Clock();
        var service = Create(repo, clock);
        var old = await service.GetDailyChallenge();
        int seed = service.GetRawSeed();
        var oldType = old.DailyType;
        var newType = oldType == DailyType.Category ? DailyType.Character : DailyType.Category;
        await service.OverrideChallengeType(newType);
        Assert.That(old.DailyType, Is.EqualTo(oldType));
        Assert.That((await service.GetDailyChallenge()).DailyType, Is.EqualTo(newType));
        clock.Now = new DateTimeOffset(2026, 9, 11, 0, 0, 0, TimeSpan.Zero);
        var current = await service.GetDailyChallenge();
        Assert.That(current.Date, Is.EqualTo(clock.Now.UtcDateTime.Date));
        Assert.That(service.GetRawSeed(), Is.EqualTo(seed + 100));
        repo.Verify(x => x.GetChallengeList(null), Times.Exactly(2));
    }

    [Test]
    public async Task StaleHistoryDoesNotDiscardValidRecencyFiltering()
    {
        var stage = DokkanConstants.Stages[0];
        var repo = new Mock<IDokkanDailyRepository>();
        repo.Setup(x => x.GetChallengeList(null)).ReturnsAsync(new[] {
            new DbChallenge { Event = null, DailyTypeName = "retired-type", LeaderFullName = "removed" },
            new DbChallenge { Event = stage.Name, Stage = stage.StageNumber, DailyTypeName = "Category", Date = DateTime.UtcNow }
        });
        var service = new RngHelperServiceV2(repo.Object, Options.Create(new DokkanDailySettings { StageRepeatLimitDays = 1 }), NullLogger<RngHelperServiceV2>.Instance);
        for (int seed = 0; seed < 100; seed++)
        {
            await service.SetDailySeed(seed);
            var challenge = await service.GetDailyChallenge();
            Assert.That(challenge.TodaysEvent, Is.Not.SameAs(stage));
            Assert.That(challenge.TodaysUnit, Is.Not.Null);
        }
    }
}
