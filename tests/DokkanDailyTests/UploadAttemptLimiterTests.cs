using DokkanDaily.Extensions;
using DokkanDaily.Models;
using DokkanDaily.Repository;
using DokkanDaily.Services;
using Microsoft.AspNetCore.Http;
using Moq;
using System.Net;

namespace DokkanDailyTests
{
    [TestFixture]
    public class UploadAttemptLimiterTests
    {
        [Test]
        public async Task SixthAttemptForTheSameUploaderAndUtcDayIsRejected()
        {
            MutableTimeProvider clock = new(new DateTimeOffset(2026, 8, 5, 12, 0, 0, TimeSpan.Zero));
            DateOnly date = new(2026, 8, 5);
            Mock<IDokkanDailyRepository> repository = new();
            repository.SetupSequence(x => x.TryAcceptUploadAttempt("discord:123456", date))
                .ReturnsAsync(true)
                .ReturnsAsync(true)
                .ReturnsAsync(true)
                .ReturnsAsync(true)
                .ReturnsAsync(true)
                .ReturnsAsync(false);
            UploadAttemptLimiter limiter = new(repository.Object, clock);

            UploadAdmission[] admissions = [];
            for (int i = 0; i < 6; i++)
                admissions = [.. admissions, await limiter.TryAcceptAsync("123456", null)];

            Assert.That(admissions.Take(5).All(x => x.Accepted), Is.True);
            Assert.That(admissions[5].Accepted, Is.False);
        }

        [Test]
        public async Task AnonymousIdentityUsesCanonicalIpAndRejectsAnUnverifiableAddress()
        {
            Mock<IDokkanDailyRepository> repository = new();
            repository.Setup(x => x.TryAcceptUploadAttempt(It.IsAny<string>(), It.IsAny<DateOnly>())).ReturnsAsync(true);
            UploadAttemptLimiter limiter = new(repository.Object, new MutableTimeProvider(
                new DateTimeOffset(2026, 8, 5, 12, 0, 0, TimeSpan.Zero)));

            UploadAdmission mapped = await limiter.TryAcceptAsync(null, "::ffff:192.0.2.40");
            UploadAdmission missing = await limiter.TryAcceptAsync(null, null);

            Assert.That(mapped.UploaderKey, Is.EqualTo("ip:192.0.2.40"));
            Assert.That(missing.Accepted, Is.False);
            repository.Verify(x => x.TryAcceptUploadAttempt("ip:192.0.2.40", new DateOnly(2026, 8, 5)), Times.Once);
        }

        [Test]
        public async Task LimitResetsAtTheUtcDayBoundary()
        {
            MutableTimeProvider clock = new(new DateTimeOffset(2026, 8, 5, 23, 59, 59, TimeSpan.Zero));
            Mock<IDokkanDailyRepository> repository = new();
            repository.Setup(x => x.TryAcceptUploadAttempt("discord:42", It.IsAny<DateOnly>())).ReturnsAsync(true);
            UploadAttemptLimiter limiter = new(repository.Object, clock);

            UploadAdmission firstDay = await limiter.TryAcceptAsync("42", null);
            clock.UtcNow = new DateTimeOffset(2026, 8, 6, 0, 0, 0, TimeSpan.Zero);
            UploadAdmission secondDay = await limiter.TryAcceptAsync("42", null);

            Assert.That(firstDay.UtcDate, Is.EqualTo(new DateOnly(2026, 8, 5)));
            Assert.That(secondDay.UtcDate, Is.EqualTo(new DateOnly(2026, 8, 6)));
        }

        [Test]
        public void ProxyProcessedRemoteAddressIsNormalizedWithoutReadingForwardedHeaders()
        {
            DefaultHttpContext context = new();
            context.Request.Headers["X-Forwarded-For"] = "198.51.100.99";
            context.Connection.RemoteIpAddress = IPAddress.Parse("::ffff:203.0.113.10");

            Assert.That(context.GetUserIpAddress(), Is.EqualTo("203.0.113.10"));

            context.Connection.RemoteIpAddress = null;
            Assert.That(context.GetUserIpAddress(), Is.Null);
        }

        private sealed class MutableTimeProvider(DateTimeOffset utcNow) : TimeProvider
        {
            public DateTimeOffset UtcNow { get; set; } = utcNow;

            public override DateTimeOffset GetUtcNow() => UtcNow;
        }
    }
}
