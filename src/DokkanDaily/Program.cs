using DokkanDaily.Components;
using DokkanDaily.Configuration;
using DokkanDaily.Repository;
using DokkanDaily.Services;

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

            builder.Services.AddHostedService<DailyResetService>();

            builder.Services.AddTransient<IRngHelperService, RngHelperService>();
            builder.Services.AddTransient<IAzureBlobService, AzureBlobService>();
            builder.Services.AddTransient<IOcrService, OcrService>();
            builder.Services.AddTransient<ISqlConnectionWrapper, SqlConnectionWrapper>();
            builder.Services.AddTransient<IDokkanDailyRepository, DokkanDailyRepository>();

            builder.Services
                .Configure<DokkanDailySettings>(builder.Configuration.GetSection(nameof(DokkanDailySettings)))
                .AddLogging(builder => builder.AddConsole());

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

            app.Run();
        }
    }
}
