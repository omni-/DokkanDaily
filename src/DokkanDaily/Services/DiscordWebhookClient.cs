using DokkanDaily.Configuration;
using DokkanDaily.Models;
using Microsoft.Extensions.Options;

namespace DokkanDaily.Services
{
    public class DiscordWebhookClient
    {
        private readonly ILogger<DiscordWebhookClient> _logger;

        private readonly HttpClient _httpClient;

        private readonly bool _isConfigured;

        public DiscordWebhookClient(ILogger<DiscordWebhookClient> logger, HttpClient httpClient, IOptions<DokkanDailySettings> settings)
        {
            _logger = logger;
            _httpClient = httpClient;

            string webhookUrl = settings.Value.WebhookUrl;
            _isConfigured = Uri.TryCreate(webhookUrl, UriKind.Absolute, out Uri baseAddress);

            // failing here would take down DI resolution for every page that touches this client,
            // so degrade to a no-op instead and say why
            if (!_isConfigured)
                _logger.LogWarning("`WebhookUrl` is missing or not an absolute URI. Discord notifications will be skipped.");
            else
                _httpClient.BaseAddress = baseAddress;
        }

        public async virtual Task PostAsync(WebhookMessage message)
        {
            if (message is null)
            {
                _logger.LogWarning("Refusing to send a null webhook message.");
                return;
            }

            await Post(message.Message, message.FilePath);
        }

        public async virtual Task PostAsync(string message)
        {
            await Post(message);
        }

        async Task Post(string message, string filePath = null)
        {
            if (!_isConfigured)
            {
                _logger.LogWarning("`WebhookUrl` is not configured. Skipping webhook request.");
                return;
            }

            if (string.IsNullOrWhiteSpace(message))
            {
                _logger.LogWarning("Refusing to send an empty webhook message.");
                return;
            }

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
                        byte[] bytes = await File.ReadAllBytesAsync($@"./wwwroot/{filePath}");
                        content.Add(new ByteArrayContent(bytes, 0, bytes.Length), "image", "image.png");
                    }
                    catch (Exception e) { _logger.LogError(e, "Failed to add file to MultiPartFormData request"); }
                }
                await _httpClient.PostAsync((string)null, content, CancellationToken.None);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unhandled error while sending webhooks request");
                throw;
            }
        }
    }
}
