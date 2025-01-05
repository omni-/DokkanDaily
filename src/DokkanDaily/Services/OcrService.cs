using DokkanDaily.Configuration;
using DokkanDaily.Exceptions;
using DokkanDaily.Models;
using DokkanDaily.Models.Enums;
using DokkanDaily.Ocr;
using DokkanDaily.Services.Interfaces;
using Microsoft.Extensions.Options;
using OpenCvSharp;
using SixLabors.ImageSharp;
using Tesseract;
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
            _logger.LogInformation("Beginning image processing...");
            byte[] arr = imageStream.ToArray();

            _logger.LogInformation("Attempting to parse as English...");
            Provider.SetParsingMode(ParsingMode.English);
            var result = TryParse(arr);
            if (result.Success) return result.ClearMetadata;
            if (result.Error != null) _logger.LogError(result.Error, "Exception encountered during parsing English");

            if (_settings.FeatureFlags.EnableJapaneseParsing)
            {
                _logger.LogInformation("English parsing failed. Attempting to parse as Japanese...");
                Provider.SetParsingMode(ParsingMode.Japanese);
                result = TryParse(arr);
                if (result.Success) return result.ClearMetadata;
                if (result.Error != null) _logger.LogError(result.Error, "Exception encountered during parsing Japanese");

                _logger.LogError("Failed both English and Japanese parse attempts.");
            }
            return null;
        }

        private ParseResult TryParse(byte[] arr)
        {
            try
            {
                var engine = Provider.CreateTesseractEngine();

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
                    throw new OcrServiceException("No contours found in image.");
                }

                var found = (from contour in contours
                             let perimeter = Cv2.ArcLength(contour, true)
                             let approx = Cv2.ApproxPolyDP(contour, 0.02 * perimeter, true)
                             where ShapeUtils.IsValidRectangle(approx, 0.8)
                             let area = Cv2.ContourArea(contour)
                             orderby area descending
                             select contour).TakeWhile((points, idx) => Cv2.ContourArea(points) > 1_000).ToArray();

                Rect boundingRect = Cv2.BoundingRect(found[0]);

                ClearScreenUI ui = new(boundingRect.Width, boundingRect.Height, Provider.BoundingBoxImagePath);

                float scaleFactor = 1750f / boundingRect.Height;
                scaleFactor = Math.Clamp(scaleFactor, 0.1f, 2f);

                // float leftPadding = boundingRect.Width / 2f + boundingRect.Width * 0.04f;
                // float rightPadding = boundingRect.Width * 0.07f;
                // Rect clearTimeRect = new Rect(boundingRect.TopLeft.Add(new Point(leftPadding, boundingRect.Height * .29f)), new Size(boundingRect.Width - leftPadding - rightPadding, boundingRect.Height * 0.05f));
                var stageClearDetailsRect = ui.GetStageClearDetailsRegion();
                var nicknameRect = ui.GetNicknameRegion();
                var clearTimeRect = ui.GetCleartimeRegion();
                var itemlessRect = ui.GetItemlessRegion();

                // Uncomment to see what the computer sees
                //Mat debugImage = t.NewMat();
                //Cv2.CvtColor(binaryBlackOnWhite, debugImage, ColorConversionCodes.GRAY2BGR);
                //Cv2.Rectangle(debugImage, boundingRect, Scalar.Red, 4);
                //Cv2.Rectangle(debugImage, nicknameRect.ToCv2Rect().Add(boundingRect.TopLeft), Scalar.Yellow, 4);
                //Cv2.Rectangle(debugImage, clearTimeRect.ToCv2Rect().Add(boundingRect.TopLeft), Scalar.Green, 4);
                //Cv2.Rectangle(debugImage, itemlessRect.ToCv2Rect().Add(boundingRect.TopLeft), Scalar.Blue, 4);
                //Cv2.Rectangle(debugImage, stageClearDetailsRect.ToCv2Rect().Add(boundingRect.TopLeft), Scalar.Pink, 4);
                //Cv2.DrawContours(debugImage, found, 0, Scalar.Red, 2, LineTypes.Link8);
                ////ShapeUtils.PreviewImage("Debug", debugImage, 5000);

                // Stage Clear Details
                string stageClearDetailsText = ParseStageClearDetails(engine, t, binaryBlackOnWhite, stageClearDetailsRect.ToCv2Rect(), boundingRect, scaleFactor);

                if (!Provider.IsValidClearHeader(stageClearDetailsText))
                {
                    // no error, simply invalid
                    return new ParseResult(false, null, null);
                }

                string nicknameText = ParseNickname(engine, t, binaryBlackOnWhite, nicknameRect.ToCv2Rect(), boundingRect, scaleFactor);

                string clearTimeText = ParseClearTime(engine, t, binaryBlackOnWhite, clearTimeRect.ToCv2Rect(), boundingRect, scaleFactor);

                bool itemless = ParseItemsUsed(engine, t, binaryBlackOnWhite, itemlessRect.ToCv2Rect(), boundingRect, scaleFactor);

                return new ParseResult(true, new()
                {
                    Nickname = nicknameText,
                    ClearTime = clearTimeText,
                    ItemlessClear = itemless
                }, null);
            }
            catch (Exception ex)
            {
                return new ParseResult(false, null, ex);
            }
        }

        private string ParseStageClearDetails(TesseractEngine engine, ResourcesTracker t, Mat binaryBlackOnWhite, Rect stageClearDetailsRect, Rect boundingRect, float scaleFactor)
        {
            Mat stageClearDetailsSection = t.T(binaryBlackOnWhite.SubMat(stageClearDetailsRect.Add(boundingRect.TopLeft)));
            Cv2.Resize(stageClearDetailsSection, stageClearDetailsSection, new Size(0, 0), scaleFactor, scaleFactor, InterpolationFlags.Linear);
            Cv2.Dilate(stageClearDetailsSection, stageClearDetailsSection, null, iterations: 1);
            Pix stageClearDetailsPix = Pix.LoadFromMemory(stageClearDetailsSection.ToBytes());
            stageClearDetailsPix.XRes = 300;
            stageClearDetailsPix.YRes = 300;

            using Page stageClearDetailsPage = engine.Process(stageClearDetailsPix, PageSegMode.SingleBlock);
            return stageClearDetailsPage.GetText().Trim();
        }

        private string ParseNickname(TesseractEngine engine, ResourcesTracker t, Mat binaryBlackOnWhite, Rect nicknameRect, Rect boundingRect, float scaleFactor)
        {
            Mat nicknameSection = t.T(binaryBlackOnWhite.SubMat(nicknameRect.Add(boundingRect.TopLeft)));
            Cv2.Resize(nicknameSection, nicknameSection, new Size(0, 0), scaleFactor, scaleFactor, InterpolationFlags.Linear);
            Cv2.Dilate(nicknameSection, nicknameSection, null, iterations: 1);
            Pix nicknamePix = Pix.LoadFromMemory(nicknameSection.ToBytes());
            nicknamePix.XRes = 300;
            nicknamePix.YRes = 300;

            using Page nicknameTextPage = engine.Process(nicknamePix, PageSegMode.SingleBlock);
            string nicknameText = nicknameTextPage.GetText().Trim();
            if (nicknameText.StartsWith("DBC *")) nicknameText = nicknameText.Replace("DBC *", "DBC*"); // one concession for the OCR
            if (nicknameText.Length == 0) nicknameText = null;

            return nicknameText;
        }

        private string ParseClearTime(TesseractEngine engine, ResourcesTracker t, Mat binaryBlackOnWhite, Rect clearTimeRect, Rect boundingRect, float scaleFactor)
        {
            Mat clearTimeSection = t.T(binaryBlackOnWhite.SubMat(clearTimeRect.Add(boundingRect.TopLeft)));
            Cv2.Resize(clearTimeSection, clearTimeSection, new Size(0, 0), scaleFactor, scaleFactor, InterpolationFlags.Linear);
            Pix clearTimePix = Pix.LoadFromMemory(clearTimeSection.ToBytes());
            clearTimePix.XRes = 300;
            clearTimePix.YRes = 300;

            engine.SetVariable("tessedit_char_whitelist", "0123456789.'\"");
            using Page clearTimeTextPage = engine.Process(clearTimePix, PageSegMode.SingleBlock);
            string clearTimeText = clearTimeTextPage.GetText().Trim();
            engine.SetVariable("tessedit_char_whitelist", "");
            if (clearTimeText.Length == 0) clearTimeText = null;

            return clearTimeText;
        }

        public bool ParseItemsUsed(TesseractEngine engine, ResourcesTracker t, Mat binaryBlackOnWhite, Rect itemlessRect, Rect boundingRect, float scaleFactor)
        {
            Mat itemlessSection = t.T(binaryBlackOnWhite.SubMat(itemlessRect.Add(boundingRect.TopLeft)));
            Cv2.Resize(itemlessSection, itemlessSection, new Size(0, 0), scaleFactor, scaleFactor, InterpolationFlags.Linear);
            Pix itemlessPix = Pix.LoadFromMemory(itemlessSection.ToBytes());
            itemlessPix.XRes = 300;
            itemlessPix.YRes = 300;

            using Page itemlessTextPage = engine.Process(itemlessPix, PageSegMode.SingleBlock);
            string itemlessText = itemlessTextPage.GetText().Trim();

            return itemlessText == Provider.None;
        }
    }
}
