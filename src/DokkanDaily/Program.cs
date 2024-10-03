using DokkanDaily.Components;
using DokkanDaily.Configuration;
using DokkanDaily.Repository;
using DokkanDaily.Services;
using DokkanDaily.Services.Interfaces;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;

namespace DokkanDaily
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // Add services to the container.
            builder.Services
                .AddRazorComponents()
                .AddInteractiveServerComponents();

            builder.Services.AddHostedService<Worker>();

            builder.Services.AddSingleton<ILeaderboardService, LeaderboardService>();

            builder.Services.AddTransient<IResetService, ResetService>();
            builder.Services.AddTransient<IRngHelperService, RngHelperService>();
            builder.Services.AddTransient<IAzureBlobService, AzureBlobService>();
            builder.Services.AddTransient<IOcrService, OcrService>();
            builder.Services.AddTransient<ISqlConnectionWrapper, SqlConnectionWrapper>();
            builder.Services.AddTransient<IDokkanDailyRepository, DokkanDailyRepository>();

            IConfigurationSection configuration = builder.Configuration.GetSection(nameof(DokkanDailySettings));

            builder.Services
                .Configure<DokkanDailySettings>(configuration)
                .AddLogging(builder => builder.AddConsole());

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

            app.Run();
        }
    }
}
