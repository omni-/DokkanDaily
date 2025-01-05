using DokkanDaily.Configuration;
using DokkanDaily.Constants;
using DokkanDaily.Models;
using DokkanDaily.Models.Enums;
using DokkanDaily.Ocr;
using DokkanDaily.Services.Interfaces;
using Microsoft.Extensions.Options;
using OpenCvSharp;
using Tesseract;
using Image = SixLabors.ImageSharp.Image;
using Rect = OpenCvSharp.Rect;
using Size = OpenCvSharp.Size;

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

                using ResourcesTracker t = new();
                Mat gray = t.NewMat();
                Cv2.CvtColor(t.T(Mat.FromImageData(arr)), gray, ColorConversionCodes.BGR2GRAY);

                Mat binary = t.NewMat();
                Cv2.Threshold(gray, binary, 100, 255, ThresholdTypes.Binary);

                Mat binaryBlackOnWhite = t.NewMat();
                Cv2.Threshold(gray, binaryBlackOnWhite, 100, 255, ThresholdTypes.BinaryInv);

                Cv2.FindContours(binary, out var contours, out var hierarchy, RetrievalModes.External, ContourApproximationModes.ApproxSimple);

                if (contours.Length <= 0)
                {
                    throw new Exception("No contours found in image.");
                }

                var found = (from contour in contours
                    let perimeter = Cv2.ArcLength(contour, true)
                    let approx = Cv2.ApproxPolyDP(contour, 0.02 * perimeter, true)
                    where ShapeUtils.IsValidRectangle(approx, 0.8)
                    let area = Cv2.ContourArea(contour)
                    orderby area descending
                    select contour).TakeWhile((points, idx) => Cv2.ContourArea(points) > 1_000).ToArray();

                Rect boundingRect = Cv2.BoundingRect(found[0]);

                ClearScreenUI ui = new ClearScreenUI(boundingRect.Width, boundingRect.Height);

                float scaleFactor = 1750f / boundingRect.Height;
                scaleFactor = Math.Clamp(scaleFactor, 0.1f, 2f);

                // float leftPadding = boundingRect.Width / 2f + boundingRect.Width * 0.04f;
                // float rightPadding = boundingRect.Width * 0.07f;
                // Rect clearTimeRect = new Rect(boundingRect.TopLeft.Add(new Point(leftPadding, boundingRect.Height * .29f)), new Size(boundingRect.Width - leftPadding - rightPadding, boundingRect.Height * 0.05f));
                var stageClearDetailsRect = ui.GetStageClearDetailsRegion();
                var nicknameRect = ui.GetNicknameRegion();
                var clearTimeRect = ui.GetCleartimeRegion();
                var itemlessRect = ui.GetItemlessRegion();

                Mat debugImage = t.NewMat();
                Cv2.CvtColor(binaryBlackOnWhite, debugImage, ColorConversionCodes.GRAY2BGR);
                Cv2.Rectangle(debugImage, boundingRect, Scalar.Red, 4);
                Cv2.Rectangle(debugImage, nicknameRect.ToCv2Rect().Add(boundingRect.TopLeft), Scalar.Red, 4);
                Cv2.Rectangle(debugImage, clearTimeRect.ToCv2Rect().Add(boundingRect.TopLeft), Scalar.Green, 4);
                Cv2.Rectangle(debugImage, itemlessRect.ToCv2Rect().Add(boundingRect.TopLeft), Scalar.Blue, 4);

                Cv2.DrawContours(debugImage, found, 0, Scalar.Red, 2, LineTypes.Link8);

                // ShapeUtils.PreviewImage("Debug", debugImage, 5000);

                // Stage Clear Details
                Mat stageClearDetailsSection = t.T(binaryBlackOnWhite.SubMat(stageClearDetailsRect.ToCv2Rect().Add(boundingRect.TopLeft)));
                Cv2.Resize(stageClearDetailsSection, stageClearDetailsSection, new Size(0, 0), scaleFactor, scaleFactor, InterpolationFlags.Linear);
                Cv2.Dilate(stageClearDetailsSection, stageClearDetailsSection, null, iterations: 1);
                Pix stageClearDetailsPix = Pix.LoadFromMemory(stageClearDetailsSection.ToBytes());
                stageClearDetailsPix.XRes = 300;
                stageClearDetailsPix.YRes = 300;

                Page stageClearDetailsPage = engine.Process(stageClearDetailsPix, PageSegMode.SingleBlock);
                string stageClearDetailsText = stageClearDetailsPage.GetText().Trim();
                stageClearDetailsPage.Dispose();
                if (!string.Equals(stageClearDetailsText.ToUpperInvariant(), OcrConstants.StageClearDetailsEng.ToUpperInvariant(), StringComparison.InvariantCulture))
                {
                    return new ParseResult(false, null, null);
                }

                // Nickname
                Mat nicknameSection = t.T(binaryBlackOnWhite.SubMat(nicknameRect.ToCv2Rect().Add(boundingRect.TopLeft)));
                Cv2.Resize(nicknameSection, nicknameSection, new Size(0, 0), scaleFactor, scaleFactor, InterpolationFlags.Linear);
                Cv2.Dilate(nicknameSection, nicknameSection, null, iterations: 1);
                Pix nicknamePix = Pix.LoadFromMemory(nicknameSection.ToBytes());
                nicknamePix.XRes = 300;
                nicknamePix.YRes = 300;

                Page nicknameTextPage = engine.Process(nicknamePix, PageSegMode.SingleBlock);
                string nicknameText = nicknameTextPage.GetText().Trim();
                nicknameTextPage.Dispose();
                if (nicknameText.StartsWith("DBC *")) nicknameText = nicknameText.Replace("DBC *", "DBC*"); // one concession for the OCR
                if (nicknameText.Length == 0) nicknameText = null;

                // Cleartime
                Mat clearTimeSection = t.T(binaryBlackOnWhite.SubMat(clearTimeRect.ToCv2Rect().Add(boundingRect.TopLeft)));
                Cv2.Resize(clearTimeSection, clearTimeSection, new Size(0, 0), scaleFactor, scaleFactor, InterpolationFlags.Linear);
                Pix clearTimePix = Pix.LoadFromMemory(clearTimeSection.ToBytes());
                clearTimePix.XRes = 300;
                clearTimePix.YRes = 300;

                engine.SetVariable("tessedit_char_whitelist", "0123456789.'\"");
                Page clearTimeTextPage = engine.Process(clearTimePix, PageSegMode.SingleBlock);
                string clearTimeText = clearTimeTextPage.GetText().Trim();
                engine.SetVariable("tessedit_char_whitelist", "");
                clearTimeTextPage.Dispose();
                if (clearTimeText.Length == 0) clearTimeText = null;

                // Itemless
                Mat itemlessSection = t.T(binaryBlackOnWhite.SubMat(itemlessRect.ToCv2Rect().Add(boundingRect.TopLeft)));
                Cv2.Resize(itemlessSection, itemlessSection, new Size(0, 0), scaleFactor, scaleFactor, InterpolationFlags.Linear);
                Pix itemlessPix = Pix.LoadFromMemory(itemlessSection.ToBytes());
                itemlessPix.XRes = 300;
                itemlessPix.YRes = 300;

                Page itemlessTextPage = engine.Process(itemlessPix, PageSegMode.SingleBlock);
                string itemlessText = itemlessTextPage.GetText().Trim();
                itemlessTextPage.Dispose();
                bool? itemless = itemlessText == Provider.None;

                if (nicknameText == null || clearTimeText == null)
                {
                    itemless = null;
                }

                return new(true,
                    new()
                    {
                        Nickname = nicknameText,
                        ClearTime = clearTimeText,
                        ItemlessClear = (bool)itemless
                    },
                    null);

                // Mat resizedBinary = t.NewMat();
                // Cv2.Resize(binary, resizedBinary, new Size(0, 0), scaleFactor, scaleFactor, InterpolationFlags.Linear);
                //
                //
                // using var img = Pix.LoadFromMemory(resizedBinary.ToBytes());
                // img.XRes = 300;
                // img.YRes = 300;
                // using var page = engine.Process(img);
                //
                // var text = Provider.GetText(page);
                //
                // if (!text.Contains(Provider.Clear) && !text.Contains(Provider.ClearAlt)) return new(false, null, null);
                //
                // List<string> split = [.. text.Split('\n', StringSplitOptions.RemoveEmptyEntries)];
                //
                // string clearTime = null;
                // bool itemless = false;
                //
                // int index = split.IndexOf(Provider.ClearTime);
                //
                // if (index == -1) index = split
                //         .IndexOf(split
                //             .FirstOrDefault(x => x
                //                 .StartsWith(Provider.ClearTimeAlt)));
                //
                // int toIndex = split.IndexOf(Provider.PersonalBest);
                //
                // if (index != -1)
                // {
                //     int to = toIndex == -1 ? 5 : toIndex - index;
                //     for (int i = 1; i < to; i++)
                //     {
                //         if (index + i >= split.Count) break;
                //
                //         string tmp = split[index + i]
                //                 .Replace('°', '"')
                //                 .Replace('*', '"')
                //                 .Replace('O', '0');
                //
                //         _logger.LogInformation("Attempting to parse `{Str}` as Dokkan-style TimeSpan...", tmp);
                //
                //         if (DokkanDailyHelper.TryParseDokkanTimeSpan(tmp, out TimeSpan clearTimeSpan))
                //         {
                //             _logger.LogInformation("Success! TimeSpan calculated as {Str}", clearTimeSpan.ToString());
                //
                //             clearTime = tmp;
                //             break;
                //         }
                //         else
                //         {
                //             _logger.LogWarning("Failed to parse `{Str}` as Dokkan-style TimeSpan", tmp);
                //         }
                //     }
                // }
                //
                // index = split.IndexOf(Provider.ItemsUsed);
                // if (index != -1 && index + 1 < split.Count)
                //     itemless = split[index + 1] == Provider.None;
                //
                // _logger.LogInformation("Calculated itemless run as `{Res}`", itemless);
                //
                // index = split.IndexOf(Provider.ItemsUsed);
                // if (index != -1 && index + 1 < split.Count)
                //     itemless = split[index + 1] == Provider.None;
                //
                // _logger.LogInformation("Calculated itemless run as `{Res}`", itemless);
                //
                // string candidate = null;
                // string[] split2 = split
                //     .FirstOrDefault(x => x
                //         .Contains(Provider.Nickname))
                //     ?.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                //
                // if (split2 != null)
                // {
                //     if (split2.Length > 1)
                //     {
                //         index = split2.ToList().IndexOf(Provider.Nickname) + 1;
                //         candidate = split2[index];
                //     }
                // }
                //
                // string nickname = candidate?.Replace("DBCe", "DBC*");
                //
                // _logger.LogInformation("Calculated nickname as `{Nick}`", nickname);
                //
                // _logger.LogInformation("OCR analysis complete.");
                //
                // return new(true,
                //     new()
                //     {
                //         ClearTime = clearTime,
                //         Nickname = nickname,
                //         ItemlessClear = itemless
                //     },
                // null);
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
