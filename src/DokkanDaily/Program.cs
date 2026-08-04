using DokkanDaily.Components;
using DokkanDaily.Configuration;
using DokkanDaily.Repository;
using DokkanDaily.Services;
using DokkanDaily.Services.Interfaces;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Components.Server.ProtectedBrowserStorage;
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

            app.MapGet("/deauth", async (context) =>
            {
                await context.SignOutAsync();
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
    }
}
