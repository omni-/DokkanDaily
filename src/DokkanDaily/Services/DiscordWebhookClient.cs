using DokkanDaily.Configuration;
using DokkanDaily.Models.Net;
using Microsoft.Extensions.Options;

namespace DokkanDaily.Services
{
    public class DiscordWebhookClient
    {
        private readonly ILogger<DiscordWebhookClient> _logger;

        private readonly HttpClient _httpClient;
        public DiscordWebhookClient(ILogger<DiscordWebhookClient> logger, HttpClient httpClient, IOptions<DokkanDailySettings> settings) 
        {
            _logger = logger;
            _httpClient = httpClient;

            _httpClient.BaseAddress = new Uri(settings.Value.WebhookUrl);
        }

        public async Task PostAsync(WebhookPayload payload)
        {
            _logger.LogInformation("Sending webhooks request {@Req}", payload);

            string s = null;
            await _httpClient.PostAsJsonAsync(s, payload, new CancellationToken());
        }
    }
}
