using DokkanDaily.Configuration;
using DokkanDaily.Helpers;
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
                // ShapeUtils.PreviewImage("Gray", gray, 0);

                Mat binaryBlackOnWhite = t.NewMat();
                Cv2.Threshold(gray, binaryBlackOnWhite, 100, 255, ThresholdTypes.BinaryInv);
                // ShapeUtils.PreviewImage("binaryBlackOnWhite", binaryBlackOnWhite, 0);

                Mat lineDetectionRegion = binaryBlackOnWhite;
                Mat edges = t.NewMat();
                Cv2.Canny(lineDetectionRegion, edges, 150, 200, 3, false);

                // Mat debugImageLines = t.NewMat();
                // Cv2.CvtColor(edges, debugImageLines, ColorConversionCodes.GRAY2BGR);
                // ShapeUtils.PreviewImage("Canny Lines", debugImageLines, 0);
                // throw new OcrServiceException("debuggin");

                LineSegmentPoint[] lines = Cv2.HoughLinesP(edges, 1, Math.PI / 180, 500, lineDetectionRegion.Width * 0.35, 0);

                // start extents at the center of our image
                int left, right, top, bottom;
                left = right = lineDetectionRegion.Width / 2;
                top = bottom = lineDetectionRegion.Height / 2;

                foreach (LineSegmentPoint lineSegmentPoint in lines)
                {
                    // loop over the lines and push out the extents when we find a horizontal or vertical line
                    bool isHorizontal = false;
                    bool isVertical = false;

                    int dy = lineSegmentPoint.P2.Y - lineSegmentPoint.P1.Y;
                    int dx = lineSegmentPoint.P2.X - lineSegmentPoint.P1.X;

                    if (dy == 0) { isHorizontal = true; }
                    else if (dx == 0) { isVertical = true; }

                    if (isHorizontal)
                    {
                        if (lineSegmentPoint.P1.Y < top) { top = lineSegmentPoint.P1.Y; }
                        if (lineSegmentPoint.P1.Y > bottom) { bottom = lineSegmentPoint.P1.Y; }
                        // Cv2.Line(debugImageLines, lineSegmentPoint.P1, lineSegmentPoint.P2, Scalar.Yellow, 1);
                    }
                    else if (isVertical)
                    {
                        if (lineSegmentPoint.P1.X < left) { left = lineSegmentPoint.P1.X; }
                        if (lineSegmentPoint.P1.X > right) { right = lineSegmentPoint.P1.X; }
                        // Cv2.Line(debugImageLines, lineSegmentPoint.P1, lineSegmentPoint.P2, Scalar.Blue, 1);
                    }
                    else
                    {
                        // Cv2.Line(debugImageLines, lineSegmentPoint.P1, lineSegmentPoint.P2, Scalar.Red, 1);
                    }
                }

                // Cv2.Line(debugImageLines, new OpenCvSharp.Point(left, top), new OpenCvSharp.Point(right, top), Scalar.Green, 2);
                // Cv2.Line(debugImageLines, new OpenCvSharp.Point(left, bottom), new OpenCvSharp.Point(right, bottom), Scalar.Green, 2);
                // Cv2.Line(debugImageLines, new OpenCvSharp.Point(left, top), new OpenCvSharp.Point(left, bottom), Scalar.Green, 2);
                // Cv2.Line(debugImageLines, new OpenCvSharp.Point(right, top), new OpenCvSharp.Point(right, bottom), Scalar.Green, 2);
                // ShapeUtils.PreviewImage("Lines", debugImageLines, 0);
                // throw new OcrServiceException("debuggin");

                Rect boundingRect = Rect.FromLTRB(left, top, right, bottom);

                ClearScreenUI ui = new(boundingRect.Width, boundingRect.Height, Provider.BoundingBoxImagePath);

                float scaleFactor = 1855f / boundingRect.Height;
                scaleFactor = Math.Clamp(scaleFactor, 0.1f, 2f);

                Rectangle stageClearDetailsRect = ui.GetStageClearDetailsRegion();
                Rectangle nicknameRect = ui.GetNicknameRegion();
                Rectangle clearTimeRect = ui.GetCleartimeRegion();
                Rectangle itemlessRect = ui.GetItemlessRegion();

                // Uncomment to see what the computer sees
                // Mat debugImage = t.NewMat();
                // Cv2.CvtColor(binaryBlackOnWhite, debugImage, ColorConversionCodes.GRAY2BGR);
                // Cv2.Rectangle(debugImage, boundingRect, Scalar.Red, 2);
                // Cv2.Rectangle(debugImage, stageClearDetailsRect.ToCv2Rect().Add(boundingRect.TopLeft), Scalar.Pink, 4);
                // Cv2.Rectangle(debugImage, nicknameRect.ToCv2Rect().Add(boundingRect.TopLeft), Scalar.Yellow, 4);
                // Cv2.Rectangle(debugImage, clearTimeRect.ToCv2Rect().Add(boundingRect.TopLeft), Scalar.Green, 4);
                // Cv2.Rectangle(debugImage, itemlessRect.ToCv2Rect().Add(boundingRect.TopLeft), Scalar.Blue, 4);
                // ShapeUtils.PreviewImage("Debug", debugImage, 0);

                // Uncomment to dump out the detected UI region image
                // Mat finalRegionToOCRFrom = t.T(Mat.FromImageData(arr)).SubMat(boundingRect);
                // finalRegionToOCRFrom.SaveImage("newReference.png");

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
            nicknameText = DokkanDailyHelper.CheckUsername(nicknameText); // some concessions for the OCR
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
