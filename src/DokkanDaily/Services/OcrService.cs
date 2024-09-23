using DokkanDaily.Constants;
using DokkanDaily.Models;
using System.Runtime.InteropServices;
using Tesseract;

namespace DokkanDaily.Services
{
    public class OcrService : IOcrService
    {
        public OcrService() 
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                TesseractEnviornment.CustomSearchPath = $"./wwwroot/lib";
        }
        public ClearMetadata ProcessImage(MemoryStream imageStream)
        {
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
            if (index != -1 && index + 1 < split.Count)
                clearTime = split[index + 1];

            index = split.IndexOf(OcrConstants.ItemsUsed);
            if (index != -1 && index + 1 < split.Count)
                itemless = split[index + 1] == OcrConstants.None;

            return new ClearMetadata()
            {
                ClearTime = clearTime?
                    .Replace('°', '"')
                    .Replace('O', '0'),
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
