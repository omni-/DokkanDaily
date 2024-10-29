using DokkanDaily.Components;
using DokkanDaily.Configuration;
using DokkanDaily.Repository;
using DokkanDaily.Services;
using DokkanDaily.Services.Interfaces;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
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

            builder.Services.AddHostedService<Worker>();

            builder.Services.AddSingleton<ILeaderboardService, LeaderboardService>();

            builder.Services.AddTransient<OcrFormatProvider>();
            builder.Services.AddTransient<IResetService, ResetService>();
            builder.Services.AddTransient<IRngHelperService, RngHelperService>();
            builder.Services.AddTransient<IAzureBlobService, AzureBlobService>();
            builder.Services.AddTransient<IOcrService, OcrService>();
            builder.Services.AddTransient<ISqlConnectionWrapper, SqlConnectionWrapper>();
            builder.Services.AddTransient<IDokkanDailyRepository, DokkanDailyRepository>();

            IConfigurationSection configuration = builder.Configuration.GetSection(nameof(DokkanDailySettings));

            builder.Services
                .Configure<DokkanDailySettings>(configuration)
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

            // Configure the HTTP request pipeline.
            if (!app.Environment.IsDevelopment())
            {
                app.UseExceptionHandler("/Error");
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
