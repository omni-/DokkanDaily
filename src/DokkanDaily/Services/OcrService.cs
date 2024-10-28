using DokkanDaily.Configuration;
using DokkanDaily.Helpers;
using DokkanDaily.Models;
using DokkanDaily.Models.Enums;
using DokkanDaily.Services.Interfaces;
using Microsoft.Extensions.Options;
using Tesseract;

namespace DokkanDaily.Services
{
    public class OcrService(ILogger<OcrService> logger, IOptions<DokkanDailySettings> settings, OcrFormatProvider ocrConstantProvider) : IOcrService
    {
        private readonly ILogger<OcrService> _logger = logger;

        private readonly DokkanDailySettings _settings = settings.Value;

        private OcrFormatProvider Provider { get; init; } = ocrConstantProvider;

        private record ParseResult(bool Success, ClearMetadata ClearMetadata, Exception Error);

        public ClearMetadata ProcessImage(MemoryStream imageStream)
        {
            _logger.LogInformation("Beginning OCR analysis...");
            byte[] arr = imageStream.ToArray();

            _logger.LogInformation("Attempting to parse as English...");
            Provider.SetParsingMode(ParsingMode.English);
            var result = TryParse(arr);
            if (result.Success) return result.ClearMetadata;
            if (result.Error != null) _logger.LogError("Exception encountered during parsing English: {Ex}", result?.Error?.Message);

            if (_settings.FeatureFlags.EnableJapaneseParsing)
            {
                _logger.LogInformation("Attempting to parse as Japanese...");
                Provider.SetParsingMode(ParsingMode.Japanese);
                result = TryParse(arr);
                if (result.Success) return result.ClearMetadata;
                if (result.Error != null) _logger.LogError("Exception encountered during parsing Japanese: {Ex}", result?.Error?.Message);

                _logger.LogError("Failed both English and Japanese parse attempts.");
            }
            return null;
        }

        private ParseResult TryParse(byte[] arr)
        {
            try
            {
                var engine = Provider.TesseractEngine;
                engine.DefaultPageSegMode = PageSegMode.SparseText;
                using var img = Pix.LoadFromMemory(arr).Scale(2.0f, 2.0f);
                using var page = engine.Process(img);

                var text = Provider.GetText(page);

                if (!text.Contains(Provider.Clear) && !text.Contains(Provider.ClearAlt)) return new(false, null, null);

                List<string> split = [.. text.Split('\n', StringSplitOptions.RemoveEmptyEntries)];

                string clearTime = null;
                bool itemless = false;

                int index = split.IndexOf(Provider.ClearTime);

                if (index == -1) index = split
                        .IndexOf(split
                            .FirstOrDefault(x => x
                                .StartsWith(Provider.ClearTimeAlt)));

                int toIndex = split.IndexOf(Provider.PersonalBest);

                if (index != -1)
                {
                    int to = toIndex == -1 ? 5 : toIndex - index;
                    for (int i = 1; i < to; i++)
                    {
                        if (index + i >= split.Count) break;

                        string tmp = split[index + i]
                                .Replace('°', '"')
                                .Replace('*', '"')
                                .Replace('O', '0');

                        _logger.LogInformation("Attempting to parse `{Str}` as Dokkan-style TimeSpan...", tmp);

                        if (DokkanDailyHelper.TryParseDokkanTimeSpan(tmp, out TimeSpan t))
                        {
                            _logger.LogInformation("Success! TimeSpan calculated as {Str}", t.ToString());

                            clearTime = tmp;
                            break;
                        }
                        else
                        {
                            _logger.LogWarning("Failed to parse `{Str}` as Dokkan-style TimeSpan", tmp);
                        }
                    }
                }

                index = split.IndexOf(Provider.ItemsUsed);
                if (index != -1 && index + 1 < split.Count)
                    itemless = split[index + 1] == Provider.None;

                _logger.LogInformation("Calculated itemless run as `{Res}`", itemless);

                index = split.IndexOf(Provider.ItemsUsed);
                if (index != -1 && index + 1 < split.Count)
                    itemless = split[index + 1] == Provider.None;

                _logger.LogInformation("Calculated itemless run as `{Res}`", itemless);

                string candidate = null;
                string[] split2 = split
                    .FirstOrDefault(x => x
                        .Contains(Provider.Nickname))
                    ?.Split(' ', StringSplitOptions.RemoveEmptyEntries);

                if (split2 != null)
                {
                    if (split2.Length > 1)
                    {
                        index = split2.ToList().IndexOf(Provider.Nickname) + 1;
                        candidate = split2[index];
                    }
                }

                string nickname = candidate?.Replace("DBCe", "DBC*");

                _logger.LogInformation("Calculated nickname as `{Nick}`", nickname);

                _logger.LogInformation("OCR analysis complete.");

                return new(true, 
                    new()
                    {
                        ClearTime = clearTime,
                        Nickname = nickname,
                        ItemlessClear = itemless
                    }, 
                null);
            }
            catch (Exception ex)
            {
                return new(false, null, ex);
            }
            finally
            {
                Provider.TesseractEngine.Dispose();
            }
        }
    }
}
