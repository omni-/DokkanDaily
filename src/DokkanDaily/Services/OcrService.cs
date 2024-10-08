﻿using DokkanDaily.Constants;
using DokkanDaily.Helpers;
using DokkanDaily.Models;
using DokkanDaily.Services.Interfaces;
using Tesseract;

namespace DokkanDaily.Services
{
    public class OcrService(ILogger<OcrService> logger) : IOcrService
    {
        private readonly ILogger<OcrService> _logger = logger;

        public ClearMetadata ProcessImage(MemoryStream imageStream)
        {
            _logger.LogInformation("Beginning OCR analysis...");
            byte[] arr = imageStream.ToArray();

            using var engine = new TesseractEngine(@"./wwwroot/tessdata", "eng", EngineMode.LstmOnly);
            engine.DefaultPageSegMode = PageSegMode.SparseText;
            using var img = Pix.LoadFromMemory(arr).Scale(2.0f, 2.0f);
            using var page = engine.Process(img);

            var text = page.GetText();

            if (!text.Contains(OcrConstants.Clear)) return null;

            List<string> split = [.. text.Split('\n', StringSplitOptions.RemoveEmptyEntries)];

            string clearTime = null;
            bool itemless = false;


            int index = split.IndexOf(OcrConstants.ClearTime);
            int toIndex = split.IndexOf(OcrConstants.PersonalBest);

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
                    
                    if (DDHelper.TryParseDokkanTimeSpan(tmp, out TimeSpan t))
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

            index = split.IndexOf(OcrConstants.ItemsUsed);
            if (index != -1 && index + 1 < split.Count)
                itemless = split[index + 1] == OcrConstants.None;

            _logger.LogInformation("Calculated itemless run as `{Res}`", itemless);

            index = split.IndexOf(OcrConstants.ItemsUsed);
            if (index != -1 && index + 1 < split.Count)
                itemless = split[index + 1] == OcrConstants.None;

            _logger.LogInformation("Calculated itemless run as `{Res}`", itemless);

            string candidate = null;
            string[] split2 = split
                .FirstOrDefault(x => x
                    .Contains(OcrConstants.Nickname))
                ?.Split(' ', StringSplitOptions.RemoveEmptyEntries);

            if (split2 != null)
            {
                if (split2.Length > 1)
                {
                    index = split2.ToList().IndexOf(OcrConstants.Nickname) + 1;
                    candidate = split2[index];
                }
            }

            string nickname = candidate?.Replace("DBCe", "DBC*");

            _logger.LogInformation("Calculated nickname as `{Nick}`", nickname);

            _logger.LogInformation("OCR analysis complete.");

            return new ClearMetadata()
            {
                ClearTime = clearTime,
                Nickname = nickname,
                ItemlessClear = itemless
            };
        }
    }
}
