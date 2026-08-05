using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using DokkanDaily.Components;
using DokkanDaily.Configuration;
using DokkanDaily.Constants;
using DokkanDaily.Repository;
using DokkanDaily.Services;
using DokkanDaily.Services.Interfaces;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Components.Server.ProtectedBrowserStorage;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;
using Serilog;

namespace DokkanDaily
{
    public class Program
    {
        public static int Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            Log.Logger = new LoggerConfiguration()
                .ReadFrom.Configuration(builder.Configuration)
                .Enrich.FromLogContext()
                .WriteTo.Console()
                .WriteTo.Debug()
                .CreateLogger();

            builder.Host.UseSerilog();

            // Add services to the container.
            builder.Services
                .AddRazorComponents()
                .AddInteractiveServerComponents();

            builder.Services.Configure<ForwardedHeadersOptions>(options =>
            {
                options.ForwardedHeaders =
                    ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;

                // App Service fronts the container with a proxy whose address we cannot enumerate,
                // so the default loopback-only trust list drops the headers entirely - which leaves
                // the app seeing http and the container IP. The default ForwardLimit of 1 still
                // makes this safe: only the rightmost entry (the one the platform appended) is
                // honoured, so a client-supplied X-Forwarded-For cannot spoof the remote address.
                options.KnownIPNetworks.Clear();
                options.KnownProxies.Clear();
            });

            builder.Services.AddHostedService<Worker>();

            builder.Services.AddSingleton<ILeaderboardService, LeaderboardService>();
            builder.Services.AddSingleton<IRngHelperService, RngHelperServiceV2>();
            builder.Services.AddSingleton<IBannerService, BannerService>();

            builder.Services.AddTransient<OcrFormatProvider>();
            builder.Services.AddTransient<IResetService, ResetService>();
            builder.Services.AddTransient<IAzureBlobService, AzureBlobService>();
            builder.Services.AddTransient<IOcrService, OcrService>();
            builder.Services.AddTransient<IDokkanDailyRepository, DokkanDailyRepository>();

            // TODO: IP tracking to enforce bans (sadface)
            builder.Services.AddScoped<ProtectedSessionStorage>();
            builder.Services.AddHttpContextAccessor();
            builder.Services.AddCascadingAuthenticationState();

            builder.Services.AddHttpClient<DiscordWebhookClient>();

            IConfigurationSection configuration = builder.Configuration.GetSection(nameof(DokkanDailySettings));

            builder.Services
                .AddOptions<DokkanDailySettings>()
                .Bind(configuration)
                .Validate(
                    settings => settings.StageRepeatLimitDays > 0 && settings.EventRepeatLimitDays > 0,
                    "StageRepeatLimitDays and EventRepeatLimitDays must be greater than zero, otherwise challenge repeat protection is silently disabled.")
                .ValidateOnStart();

            ConfigureDataProtection(builder, configuration);

            builder.Services
                .AddAuthentication(opt =>
                {
                    opt.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
                    opt.DefaultSignInScheme = CookieAuthenticationDefaults.AuthenticationScheme;
                })
                .AddCookie()
                .AddDiscord(opt =>
                {
                    opt.AppId = configuration[nameof(DokkanDailySettings.OAuth2ClientId)];
                    opt.AppSecret = configuration[nameof(DokkanDailySettings.OAuth2ClientSecret)];

                    opt.SaveTokens = true;
                });

            var app = builder.Build();

            app.UseForwardedHeaders();

            // Configure the HTTP request pipeline.
            if (!app.Environment.IsDevelopment())
            {
                app.UseExceptionHandler("/Error", createScopeForErrors: true);
                // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
                app.UseHsts();
            }
            else
            {
                app.UseDeveloperExceptionPage();
            }

            app.UseHttpsRedirection();

            app.UseStaticFiles();
            app.UseAntiforgery();

            app.UseSerilogRequestLogging();

            app.MapRazorComponents<App>()
                .AddInteractiveServerRenderMode();

            app.MapGet("/", context =>
            {
                context.Response.Redirect("/daily");
                return Task.CompletedTask;
            });

            app.MapGet("/auth", async (context) =>
            {
                await context.ChallengeAsync("Discord", new AuthenticationProperties { RedirectUri = "/" });
            });

            // Redirects rather than ending on a blank page, so logging out can be a plain link.
            // SignOutAsync only writes headers, so the response has not started and can still be
            // turned into a 302.
            app.MapGet("/deauth", async (context) =>
            {
                await context.SignOutAsync();
                context.Response.Redirect("/daily");
            });

            Log.Information("Starting web host");
            try
            {
                app.Run();
                return 0;
            }
            catch (Exception ex)
            {
                Log.Fatal(ex, "Host terminated unexpectedly");
                return 1;
            }
            finally
            {
                Log.CloseAndFlush();
            }
        }

        /// <summary>
        /// Persists the Data Protection key ring to blob storage.
        /// </summary>
        /// <remarks>
        /// The default provider writes the key ring to the container filesystem, which is discarded
        /// on every redeploy, restart and scale event. A fresh key ring cannot decrypt anything the
        /// previous instance issued: antiforgery tokens fail to deserialize, every signed-in user is
        /// silently logged out, and <see cref="ProtectedSessionStorage"/> throws on reads of data it
        /// wrote itself. Sharing one key ring also lets scaled-out instances read each other's
        /// cookies, which they otherwise cannot.
        /// </remarks>
        private static void ConfigureDataProtection(WebApplicationBuilder builder, IConfigurationSection configuration)
        {
            string connectionString = configuration[nameof(DokkanDailySettings.AzureBlobConnectionString)];

            if (string.IsNullOrWhiteSpace(connectionString))
            {
                // Expected when running locally without storage configured. The filesystem key ring
                // is durable there, so this only costs cookie continuity across a container rebuild.
                Log.Warning(
                    "{Setting} is not configured, so the Data Protection key ring falls back to local storage. " +
                    "In a container this means auth cookies and antiforgery tokens are invalidated on every restart.",
                    nameof(DokkanDailySettings.AzureBlobConnectionString));

                return;
            }

            BlobContainerClient container = new(connectionString, AzureConstants.DATA_PROTECTION_CONTAINER);

            try
            {
                // The key ring is written lazily, so a missing container would otherwise surface as a
                // failure on the first sign-in rather than here.
                container.CreateIfNotExists(PublicAccessType.None);
            }
            catch (Exception ex)
            {
                // Registering the provider anyway would be worse than not registering it: Data
                // Protection would throw on the first protect instead of only losing continuity, and
                // that breaks antiforgery on every request. Fall back to the ephemeral key ring, which
                // is what the app did before this was configured at all, and stay serving.
                Log.Error(
                    ex,
                    "Could not open the Data Protection container `{Container}`. Falling back to an ephemeral key ring: " +
                    "sign-ins will not survive a restart, and instances will not be able to read each other's cookies.",
                    AzureConstants.DATA_PROTECTION_CONTAINER);

                return;
            }

            builder.Services
                .AddDataProtection()
                .SetApplicationName(AzureConstants.DATA_PROTECTION_APP_NAME)
                .PersistKeysToAzureBlobStorage(container.GetBlobClient(AzureConstants.DATA_PROTECTION_BLOB));

            Log.Information(
                "Data Protection key ring persisting to `{Container}/{Blob}`.",
                AzureConstants.DATA_PROTECTION_CONTAINER,
                AzureConstants.DATA_PROTECTION_BLOB);
        }
    }
}
