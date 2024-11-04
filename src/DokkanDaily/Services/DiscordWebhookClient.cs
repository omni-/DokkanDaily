using DokkanDaily.Configuration;
using DokkanDaily.Models;
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

        public async Task PostAsync(WebhookMessage message)
        {
            await Post(message.Message, message.FilePath);
        }

        public async Task PostAsync(string message)
        {
            await Post(message);
        }

        async Task Post(string message, string filePath = null)
        {
            _logger.LogInformation("Sending webhooks request: {Msg}", message);
            try
            {
                MultipartFormDataContent content = new()
                {
                    { new StringContent(message), "content" }
                };
                if (!string.IsNullOrEmpty(filePath))
                {
                    try
                    {
                        var bytes = File.ReadAllBytes($@"./wwwroot/{filePath}");
                        content.Add(new ByteArrayContent(bytes, 0, bytes.Length), "image", "image.png");
                    }
                    catch (Exception e) { _logger.LogError(e, "Failed to add file to MultiPartFormData request"); }
                }
                await _httpClient.PostAsync((string)null, content, new CancellationToken());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unhandled error while sending webhooks request");
                throw;
            }
        }
    }
}
