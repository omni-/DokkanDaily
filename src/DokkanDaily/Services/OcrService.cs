using DokkanDaily.Constants;
using DokkanDaily.Helpers;
using DokkanDaily.Models;
using DokkanDaily.Services.Interfaces;
using OpenCvSharp;
using Tesseract;

namespace DokkanDaily.Services
{
    public class OcrService(ILogger<OcrService> logger) : IOcrService
    {
        private readonly ILogger<OcrService> _logger = logger;

        public static void previewImage(String title, Mat image)
        {
            Size screenResolution = new Size(2560, 1440); // idk how to actually get this without adding packages

            Size imageSize = image.Size();
            if (imageSize.Height > screenResolution.Height)
            {
                image = image.Clone(); // so we don't modify the image that was passed in
                // scale down the image if it's bigger than the screen height
                double scalingFactor = (double)screenResolution.Height / (double)imageSize.Height;
                scalingFactor *= 0.9; // make it a little smaller than the exact screen height
                Cv2.Resize(image, image, new Size(), scalingFactor, scalingFactor);
            }

            Cv2.ImShow(title, image);
            Cv2.MoveWindow(title, screenResolution.Width / 2 - image.Size().Width / 2, 0);
            Cv2.WaitKey(0);
            Cv2.DestroyWindow(title);
        }

        public ClearMetadata ProcessImage(MemoryStream imageStream)
        {
            _logger.LogInformation("Beginning OCR analysis...");

            byte[] arr = imageStream.ToArray();

            Mat m = Cv2.ImDecode(arr, ImreadModes.Grayscale);
            Cv2.Threshold(m, m, 100, 255, ThresholdTypes.Binary);
            Cv2.Resize(m, m, new Size(), .99f, .99f);
            Cv2.BitwiseNot(m, m);
            previewImage("peepee", m);

            using var engine = new TesseractEngine(@"./wwwroot/tessdata", "eng", EngineMode.LstmOnly);
            engine.DefaultPageSegMode = PageSegMode.SparseText;

            using var img = Pix
                .LoadFromMemory(m.ImEncode());

            img.Save("C:/users/cooper/downloads/tmp.png");
            using var page = engine.Process(img);

            var text = page.GetText();

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

            string candidate = null;
            string[] split2 = split
                .FirstOrDefault(x => x
                    .Contains(OcrConstants.Nickname))
                ?.Split(' ', StringSplitOptions.RemoveEmptyEntries);

            if(split2 != null)
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
