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
                using var engine = Provider.CreateTesseractEngine();

                using ResourcesTracker t = new();
                Mat gray = t.NewMat();
                Cv2.CvtColor(t.T(Mat.FromImageData(arr)), gray, ColorConversionCodes.BGR2GRAY);
                // ShapeUtils.PreviewImage("Gray", gray, 0);

                Mat binaryBlackOnWhite = t.NewMat();
                Cv2.Threshold(gray, binaryBlackOnWhite, 100, 255, ThresholdTypes.BinaryInv);
                // ShapeUtils.PreviewImage("binaryBlackOnWhite", binaryBlackOnWhite, 0);

                Mat brightWhiteOnBlack = t.NewMat();
                Cv2.Threshold(gray, brightWhiteOnBlack, 180, 255, ThresholdTypes.Binary);

                ParseResult darkLineResult = TryParseWithLineDetectionRegion(engine, t, binaryBlackOnWhite, binaryBlackOnWhite, useRelaxedLineDetection: false);
                if (darkLineResult.Success)
                {
                    return darkLineResult;
                }

                if (!Provider.IsJapanese)
                {
                    return darkLineResult;
                }

                Mat brightAndDarkLineDetectionRegion = t.NewMat();
                Cv2.BitwiseOr(binaryBlackOnWhite, brightWhiteOnBlack, brightAndDarkLineDetectionRegion);

                ParseResult brightLineResult = TryParseWithLineDetectionRegion(engine, t, binaryBlackOnWhite, brightAndDarkLineDetectionRegion, useRelaxedLineDetection: true);
                if (brightLineResult.Success || brightLineResult.Error != null)
                {
                    return brightLineResult;
                }

                return darkLineResult;
            }
            catch (Exception ex)
            {
                return new ParseResult(false, null, ex);
            }
        }

        private ParseResult TryParseWithLineDetectionRegion(TesseractEngine engine, ResourcesTracker t, Mat binaryBlackOnWhite, Mat lineDetectionRegion, bool useRelaxedLineDetection)
        {
            try
            {
                Mat edges = t.NewMat();
                Cv2.Canny(lineDetectionRegion, edges, 150, 200, 3, false);

                // Mat debugImageLines = t.NewMat();
                // Cv2.CvtColor(edges, debugImageLines, ColorConversionCodes.GRAY2BGR);
                // ShapeUtils.PreviewImage("Canny Lines", debugImageLines, 0);
                // throw new OcrServiceException("debuggin");

                double minimumLineLength = lineDetectionRegion.Width * 0.35;
                int houghThreshold = useRelaxedLineDetection
                    ? Math.Min(500, (int)(lineDetectionRegion.Width * 0.25))
                    : 500;
                LineSegmentPoint[] lines = Cv2.HoughLinesP(edges, 1, Math.PI / 180, houghThreshold, minimumLineLength, 0);

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

                    if (useRelaxedLineDetection)
                    {
                        if (IsNearlyHorizontal(dx, dy)) { isHorizontal = true; }
                        else if (IsNearlyVertical(dx, dy)) { isVertical = true; }
                    }
                    else
                    {
                        if (dy == 0) { isHorizontal = true; }
                        else if (dx == 0) { isVertical = true; }
                    }

                    if (isHorizontal)
                    {
                        int lineTop = useRelaxedLineDetection
                            ? Math.Min(lineSegmentPoint.P1.Y, lineSegmentPoint.P2.Y)
                            : lineSegmentPoint.P1.Y;
                        int lineBottom = useRelaxedLineDetection
                            ? Math.Max(lineSegmentPoint.P1.Y, lineSegmentPoint.P2.Y)
                            : lineSegmentPoint.P1.Y;
                        if (lineTop < top) { top = lineTop; }
                        if (lineBottom > bottom) { bottom = lineBottom; }
                        // Cv2.Line(debugImageLines, lineSegmentPoint.P1, lineSegmentPoint.P2, Scalar.Yellow, 1);
                    }
                    else if (isVertical)
                    {
                        int lineLeft = useRelaxedLineDetection
                            ? Math.Min(lineSegmentPoint.P1.X, lineSegmentPoint.P2.X)
                            : lineSegmentPoint.P1.X;
                        int lineRight = useRelaxedLineDetection
                            ? Math.Max(lineSegmentPoint.P1.X, lineSegmentPoint.P2.X)
                            : lineSegmentPoint.P1.X;
                        if (lineLeft < left) { left = lineLeft; }
                        if (lineRight > right) { right = lineRight; }
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
                //Mat debugImage = t.NewMat();
                //Cv2.CvtColor(binaryBlackOnWhite, debugImage, ColorConversionCodes.GRAY2BGR);
                //Cv2.Rectangle(debugImage, boundingRect, Scalar.Red, 2);
                //Cv2.Rectangle(debugImage, stageClearDetailsRect.ToCv2Rect().Add(boundingRect.TopLeft), Scalar.Pink, 4);
                //Cv2.Rectangle(debugImage, nicknameRect.ToCv2Rect().Add(boundingRect.TopLeft), Scalar.Yellow, 4);
                //Cv2.Rectangle(debugImage, clearTimeRect.ToCv2Rect().Add(boundingRect.TopLeft), Scalar.Green, 4);
                //Cv2.Rectangle(debugImage, itemlessRect.ToCv2Rect().Add(boundingRect.TopLeft), Scalar.Blue, 4);
                //ShapeUtils.PreviewImage("Debug", debugImage, 0);

                // Uncomment to dump out the detected UI region image
                //Mat finalRegionToOCRFrom = t.T(Mat.FromImageData(arr)).SubMat(boundingRect);
                //finalRegionToOCRFrom.SaveImage("newReference.png");

                // Stage Clear Details
                string stageClearDetailsText = ParseStageClearDetails(engine, t, binaryBlackOnWhite, stageClearDetailsRect.ToCv2Rect(), boundingRect, scaleFactor);

                if (!Provider.IsValidClearHeader(stageClearDetailsText))
                {
                    // no error, simply invalid
                    return new ParseResult(false, null, null);
                }

                string nicknameText = ParseNickname(engine, t, binaryBlackOnWhite, nicknameRect.ToCv2Rect(), boundingRect, scaleFactor);

                string clearTimeText = ParseClearTime(engine, t, binaryBlackOnWhite, clearTimeRect.ToCv2Rect(), boundingRect, scaleFactor);

                if (nicknameText == null || clearTimeText == null)
                {
                    return new ParseResult(false, null, null);
                }

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

        private static bool IsNearlyHorizontal(int dx, int dy)
        {
            int tolerance = Math.Max(2, (int)(Math.Abs(dx) * 0.02));
            return Math.Abs(dx) > Math.Abs(dy) && Math.Abs(dy) <= tolerance;
        }

        private static bool IsNearlyVertical(int dx, int dy)
        {
            int tolerance = Math.Max(2, (int)(Math.Abs(dy) * 0.02));
            return Math.Abs(dy) > Math.Abs(dx) && Math.Abs(dx) <= tolerance;
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
