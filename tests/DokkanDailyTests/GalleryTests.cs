using System.Security.Claims;
using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using DokkanDaily.Components.Pages;
using DokkanDaily.Constants;
using DokkanDaily.Models;
using DokkanDaily.Models.Enums;
using DokkanDaily.Services.Interfaces;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.HtmlRendering.Infrastructure;
using Microsoft.AspNetCore.Components.Server.ProtectedBrowserStorage;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;
using Moq;

namespace DokkanDailyTests;

public class GalleryTests
{
    [Test]
    public async Task RenderedGalleryAllowsUnreadableTitlesButHidesUnrecognizedUploads()
    {
        BlobClient Blob(string name, string status, string validation, string invalid = null, bool recognized = true)
        {
            Dictionary<string, string> metadata = new();
            if (recognized)
            {
                metadata[AzureConstants.USER_NAME_TAG] = "Tester";
                metadata[AzureConstants.CLEAR_TIME_TAG] = "0'01\"00.0";
                metadata[AzureConstants.ITEMLESS_TAG] = "true";
            }
            if (status is not null) metadata[AzureConstants.UPLOAD_STATUS_TAG] = status;
            if (validation is not null) metadata[AzureConstants.STAGE_VALIDATION_TAG] = validation;
            if (invalid is not null) metadata[AzureConstants.INVALID_TAG] = invalid;
            var blob = new Mock<BlobClient>();
            blob.SetupGet(x => x.Name).Returns(name);
            blob.SetupGet(x => x.Uri).Returns(new Uri("https://storage.invalid/clears/" + name));
            blob.Setup(x => x.GetTagsAsync(It.IsAny<BlobRequestConditions>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(Response.FromValue(BlobsModelFactory.GetBlobTagResult(new Dictionary<string, string>()), Mock.Of<Response>()));
            blob.Setup(x => x.GetPropertiesAsync(It.IsAny<BlobRequestConditions>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(Response.FromValue(BlobsModelFactory.BlobProperties(metadata: metadata), Mock.Of<Response>()));
            return blob.Object;
        }

        var storage = new Mock<IAzureBlobService>();
        storage.Setup(x => x.GetFilesForTag(It.IsAny<string>(), It.IsAny<string>())).ReturnsAsync([
            Blob("confirmed.png", "valid", "match"),
            Blob("unreadable-title.png", "valid", "unknown"),
            Blob("incident.jpeg", "valid", "unknown", recognized: false),
            Blob("no-clear-data.png", "valid", "match", recognized: false),
            Blob("legacy.png", null, null),
            Blob("pending.png", "pending", "match"),
            Blob("invalid.png", "valid", "match", "true")
        ]);
        storage.Setup(x => x.GetBlobSasTokenByFile(It.IsAny<string>(), It.IsAny<string>())).Returns("sig=test");
        var rng = new Mock<IRngHelperService>();
        rng.Setup(x => x.GetDailyChallenge()).ReturnsAsync(new Challenge(DailyType.Category,
            DokkanConstants.Stages[0], null, new Category("Test", Tier.Z), null, null, DateTime.UtcNow));
        var auth = new Mock<AuthenticationStateProvider>();
        auth.Setup(x => x.GetAuthenticationStateAsync()).ReturnsAsync(new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity())));

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddAuthorizationCore();
        services.AddSingleton(storage.Object);
        services.AddSingleton(rng.Object);
        services.AddSingleton(auth.Object);
        services.AddSingleton<IHttpContextAccessor>(new HttpContextAccessor { HttpContext = new DefaultHttpContext() });
        services.AddSingleton<IJSRuntime>(Mock.Of<IJSRuntime>());
        services.AddSingleton<NavigationManager>(new GalleryNavigationManager());
        services.AddSingleton<IBannerService>(Mock.Of<IBannerService>());
        services.AddSingleton<IDataProtectionProvider>(new EphemeralDataProtectionProvider());
        services.AddScoped<ProtectedSessionStorage>();
        using var provider = services.BuildServiceProvider();
        await using var renderer = new GalleryHtmlRenderer(provider, provider.GetRequiredService<ILoggerFactory>());
        string html = await renderer.Dispatcher.InvokeAsync(async () =>
        {
            var root = renderer.BeginRenderingComponent(typeof(Clears), ParameterView.Empty);
            await root.QuiescenceTask;
            return root.ToHtmlString();
        });

        Assert.That(html, Does.Contain("https://storage.invalid/clears/confirmed.png"));
        Assert.That(html, Does.Contain("https://storage.invalid/clears/unreadable-title.png"));
        Assert.That(html, Does.Contain("https://storage.invalid/clears/legacy.png"));
        foreach (string hidden in new[] { "incident.jpeg", "no-clear-data.png", "pending.png", "invalid.png" })
            Assert.That(html, Does.Not.Contain("https://storage.invalid/clears/" + hidden));
        storage.Verify(x => x.GetBlobSasTokenByFile("incident.jpeg", It.IsAny<string>()), Times.Never);
    }

    private sealed class GalleryNavigationManager : NavigationManager
    {
        public GalleryNavigationManager() => Initialize("https://dokkan.invalid/", "https://dokkan.invalid/clears");
    }

    // Exercise the real page's rendering without requiring an interactive server circuit.
    private sealed class GalleryHtmlRenderer(IServiceProvider services, ILoggerFactory loggerFactory)
        : StaticHtmlRenderer(services, loggerFactory)
    {
        protected override IComponent ResolveComponentForRenderMode(Type componentType, int? parentComponentId,
            IComponentActivator componentActivator, IComponentRenderMode renderMode) => componentActivator.CreateInstance(componentType);
    }
}
