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
            Assert.That(admissions[5].RejectionMessage, Does.Contain("five upload attempts").And.Contain("UTC"));
        }

        [Test]
        public async Task AuthenticatedIdentityUsesDiscordIdInsteadOfIp()
        {
            Mock<IDokkanDailyRepository> repository = new();
            repository.Setup(x => x.TryAcceptUploadAttempt(It.IsAny<string>(), It.IsAny<DateOnly>())).ReturnsAsync(true);
            UploadAttemptLimiter limiter = new(repository.Object, new MutableTimeProvider(
                new DateTimeOffset(2026, 8, 5, 12, 0, 0, TimeSpan.Zero)));

            UploadAdmission admission = await limiter.TryAcceptAsync(" 987654 ", "203.0.113.7");

            Assert.That(admission.Accepted, Is.True);
            Assert.That(admission.UploaderKey, Is.EqualTo("discord:987654"));
            repository.Verify(x => x.TryAcceptUploadAttempt("discord:987654", admission.UtcDate), Times.Once);
            repository.Verify(x => x.TryAcceptUploadAttempt("ip:203.0.113.7", It.IsAny<DateOnly>()), Times.Never);
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
            UploadAdmission unspecified = await limiter.TryAcceptAsync(null, "0.0.0.0");

            Assert.That(mapped.UploaderKey, Is.EqualTo("ip:192.0.2.40"));
            Assert.That(missing.Accepted, Is.False);
            Assert.That(unspecified.Accepted, Is.False);
            Assert.That(missing.RejectionMessage, Does.Contain("verify your network address"));
            repository.Verify(x => x.TryAcceptUploadAttempt("ip:192.0.2.40", new DateOnly(2026, 8, 5)), Times.Once);
            repository.Verify(x => x.TryAcceptUploadAttempt(It.IsAny<string>(), It.IsAny<DateOnly>()), Times.Once);
        }

        [Test]
        public async Task LimitResetsAtTheUtcDayBoundary()
        {
            MutableTimeProvider clock = new(new DateTimeOffset(2026, 8, 5, 23, 59, 59, TimeSpan.Zero));
            DateOnly firstDay = new(2026, 8, 5);
            DateOnly secondDay = firstDay.AddDays(1);
            Mock<IDokkanDailyRepository> repository = new();
            repository.SetupSequence(x => x.TryAcceptUploadAttempt("discord:42", firstDay))
                .ReturnsAsync(true)
                .ReturnsAsync(true)
                .ReturnsAsync(true)
                .ReturnsAsync(true)
                .ReturnsAsync(true)
                .ReturnsAsync(false);
            repository.Setup(x => x.TryAcceptUploadAttempt("discord:42", secondDay)).ReturnsAsync(true);
            UploadAttemptLimiter limiter = new(repository.Object, clock);

            for (int i = 0; i < 5; i++)
                Assert.That((await limiter.TryAcceptAsync("42", null)).Accepted, Is.True);

            Assert.That((await limiter.TryAcceptAsync("42", null)).Accepted, Is.False);

            clock.UtcNow = new DateTimeOffset(2026, 8, 6, 0, 0, 0, TimeSpan.Zero);
            UploadAdmission nextDay = await limiter.TryAcceptAsync("42", null);

            Assert.That(nextDay.Accepted, Is.True);
            Assert.That(nextDay.UtcDate, Is.EqualTo(new DateOnly(2026, 8, 6)));
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

        [Test]
        public void EquivalentIpv6RepresentationsNormalizeToTheSameKey()
        {
            bool expandedParsed = UploadAttemptLimiter.TryNormalizeIpAddress(
                "2001:0db8:0000:0000:0000:0000:0000:0001", out string expanded);
            bool compressedParsed = UploadAttemptLimiter.TryNormalizeIpAddress(
                "2001:db8::1", out string compressed);

            Assert.That(expandedParsed, Is.True);
            Assert.That(compressedParsed, Is.True);
            Assert.That(expanded, Is.EqualTo("2001:db8::1"));
            Assert.That(compressed, Is.EqualTo(expanded));
        }

        private sealed class MutableTimeProvider(DateTimeOffset utcNow) : TimeProvider
        {
            public DateTimeOffset UtcNow { get; set; } = utcNow;

            public override DateTimeOffset GetUtcNow() => UtcNow;
        }

    }
}
