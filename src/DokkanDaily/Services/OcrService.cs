using DokkanDaily.Constants;
using DokkanDaily.Helpers;
using DokkanDaily.Models;
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

            List<string> split = [.. text.Split('\n', StringSplitOptions.RemoveEmptyEntries)];

            string clearTime = null;
            bool itemless = false;

            int index = split.IndexOf(OcrConstants.ClearTime);
            if (index != -1)
            {
                for (int i = 1; i < 4; i++)
                {
                    if (index + i >= split.Count) break;

                    string tmp = split[index + i]
                            .Replace('°', '"')
                            .Replace('*', '"')
                            .Replace('O', '0');

                    if (DDHelper.TryParseDokkanTimeSpan(tmp, out _))
                    {
                        clearTime = tmp;
                        break;
                    }
                }
            }

            index = split.IndexOf(OcrConstants.ItemsUsed);
            if (index != -1 && index + 1 < split.Count)
                itemless = split[index + 1] == OcrConstants.None;

            _logger.LogInformation("OCR analysis complete.");

            return new ClearMetadata()
            {
                ClearTime = clearTime,
                Nickname = split
                    .FirstOrDefault(x => x
                    .StartsWith(OcrConstants.Nickname))?
                    .Replace(OcrConstants.Nickname, string.Empty)?
                    .Replace("DBCe", "DBC*"),
                ItemlessClear = itemless
            };
        }
    }
}
