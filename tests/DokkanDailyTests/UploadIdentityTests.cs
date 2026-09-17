using DokkanDaily.Extensions;
using DokkanDaily.Helpers;
using Microsoft.AspNetCore.Http;
using System.Net;

namespace DokkanDailyTests;

public class UploadIdentityTests
{
    [TestCase("::ffff:192.0.2.40", "192.0.2.40")]
    [TestCase("192.0.2.40", "192.0.2.40")]
    [TestCase(null, null)]
    [TestCase("0.0.0.0", null)]
    [TestCase("::", null)]
    public void ReplacementIdentityRequiresCanonicalUsableIp(string input, string expected)
    {
        Assert.That(UploadIdentity.TryNormalizeIpAddress(input, out var actual), Is.EqualTo(expected is not null));
        Assert.That(actual, Is.EqualTo(expected));
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

}
