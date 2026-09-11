using System.Text.RegularExpressions;
using DokkanDaily.Services;
using OpenCvSharp;
using Tesseract;
using Rect = OpenCvSharp.Rect;

namespace DokkanDaily.Ocr;

internal sealed record StageTitleReading(string EventTitle, string StageTitle, Rect Bounds, Rect[] Lines, bool CandidateSelected, bool Fallback);

// Frozen ObservationV2 v3 geometry. No challenge, aliases, or IDs enter recognition.
internal static class StageTitleReader
{
    private static readonly Regex JapaneseScript = new("[\u3040-\u30ff\u3400-\u4dbf\u4e00-\u9fff]");
    internal static StageTitleReading Read(byte[] image, OcrFormatProvider provider)
    {
        string modelDirectory = provider.TrainDataPath;
        using var color = Mat.FromImageData(image);
        using var eng = new TesseractEngine(modelDirectory, "eng", EngineMode.LstmOnly);
        using var jpn = new TesseractEngine(modelDirectory, "jpn", EngineMode.LstmOnly);
        var bounds = DetectBounds(color, false);
        if (provider.IsJapanese && !HeaderValid(color, bounds, jpn, provider)) bounds = DetectBounds(color, true);
        var engine = provider.IsJapanese ? jpn : eng;
        var lines = Propose(color, bounds);
        string Read(Rect rect, TesseractEngine reader, bool adaptedInput, bool block = false)
        {
            // Frozen v3 diagnostic: reduce automatic candidate line padding
            // from three pixels to two. Routing/fallback use original crops.
            if (adaptedInput && !block && rect.Width > 2 && rect.Height > 2)
                rect = new Rect(rect.X + 1, rect.Y + 1, rect.Width - 2, rect.Height - 2);
            if (rect.X < 0 || rect.Y < 0 || rect.Right > color.Width || rect.Bottom > color.Height || rect.Width < 1 || rect.Height < 1) return "";
            using var region = color.SubMat(rect); using var gray = new Mat(); Cv2.CvtColor(region, gray, ColorConversionCodes.BGR2GRAY);
            if (adaptedInput) Cv2.Threshold(gray, gray, 180, 255, ThresholdTypes.BinaryInv); else Cv2.BitwiseNot(gray, gray);
            if (!adaptedInput) { var scale = Math.Clamp(1855f / Math.Max(1, bounds.Height), .1f, 2f); Cv2.Resize(gray, gray, new Size(), scale, scale, InterpolationFlags.Linear); }
            Cv2.CopyMakeBorder(gray, gray, 10, 10, 10, 10, BorderTypes.Constant, Scalar.White);
            using var pix = Pix.LoadFromMemory(gray.ToBytes()); pix.XRes = pix.YRes = 300;
            using var page = reader.Process(pix, block ? PageSegMode.SingleBlock : adaptedInput ? PageSegMode.RawLine : PageSegMode.SingleLine);
            return page.GetText().Trim();
        }
        string[] Titles(TesseractEngine reader, bool adaptedInput, out bool fallback)
        {
            var text = lines.Select(rect => Read(rect, reader, adaptedInput)).ToArray();
            var wrapped = text.Length == 3 && text[1].StartsWith('[') && text[1].EndsWith(']');
            fallback = text.Length != 2 && !wrapped;
            if (!fallback) return new[] { wrapped ? text[0] + "\n" + text[1] : text[0], text[^1] };
            Rect Band(double y, double h) => new(bounds.X + (int)(.04 * bounds.Width), bounds.Y + (int)(y * bounds.Height), (int)(.92 * bounds.Width), (int)(h * bounds.Height));
            return new[] { Read(Band(.08, .046), engine, false, true), Read(Band(.126, .044), engine, false, true) };
        }
        var titles = Titles(engine, false, out var fallback);
        var script = JapaneseScript.IsMatch(string.Join("\n", titles));
        var useCandidate = provider.IsJapanese && script;
        if (useCandidate)
        {
            using var adapted = new TesseractEngine(modelDirectory, "jpn-stage-v2", EngineMode.LstmOnly);
            titles = Titles(adapted, true, out fallback);
        }
        return new(titles[0], titles[1], bounds, lines.ToArray(), useCandidate, fallback);
    }
    private static List<Rect> Propose(Mat color, Rect bounds)
    {
        var result = new List<Rect>(); if (bounds.Width <= 0 || bounds.Height <= 0) return result;
        int left = (int)(color.Width * .035), right = (int)(color.Width * .965), top = Math.Max(0, bounds.Y + (int)(.075 * bounds.Height)), bottom = Math.Min(color.Height, bounds.Y + (int)(.185 * bounds.Height));
        if (bottom <= top || right <= left) return result;
        using var region = color.SubMat(Rect.FromLTRB(left, top, right, bottom)); using var gray = new Mat(); Cv2.CvtColor(region, gray, ColorConversionCodes.BGR2GRAY);
        var mask = new bool[gray.Height, gray.Width]; var counts = new int[gray.Height];
        for (int x = 0; x < gray.Width; x++)
        {
            int n = 0; for (int y = 0; y < gray.Height; y++) if (gray.At<byte>(y, x) >= 180) n++;
            if (n > gray.Height * .85) continue;
            for (int y = 0; y < gray.Height; y++) if (gray.At<byte>(y, x) >= 180) { mask[y, x] = true; counts[y]++; }
        }
        var runs = new List<(int start, int end)>(); int start = -1, last = 0;
        for (int y = 0; y < gray.Height + 3; y++)
        {
            if (y < gray.Height && counts[y] > 1) { if (start < 0) start = y; last = y; }
            if (start >= 0 && y - last > 2) { if (last - start + 1 >= Math.Max(4, (int)(bounds.Height * .006))) runs.Add((start, last + 1)); start = -1; }
        }
        if (runs.Count is not (2 or 3)) return result;
        foreach (var run in runs)
        {
            int min = gray.Width, max = 0;
            for (int y = run.start; y < run.end; y++) for (int x = 0; x < gray.Width; x++) if (mask[y, x]) { min = Math.Min(min, x); max = Math.Max(max, x); }
            result.Add(Rect.FromLTRB(left + Math.Max(0, min - 3), top + Math.Max(0, run.start - 3), left + Math.Min(gray.Width, max + 4), top + Math.Min(gray.Height, run.end + 3)));
        }
        return result;
    }

    // Same offline extraction geometry as frozen Program.cs baseline.
    static bool HeaderValid(Mat color, OpenCvSharp.Rect bounds, TesseractEngine engine, OcrFormatProvider provider)
    {
        try
        {
            var ui = new ClearScreenUI(bounds.Width, bounds.Height, provider.BoundingBoxImagePath);
            var r = ui.GetStageClearDetailsRegion();
            using var crop = color.SubMat(new OpenCvSharp.Rect(bounds.X + r.X, bounds.Y + r.Y, r.Width, r.Height));
            using var input = new Mat();
            Cv2.CvtColor(crop, input, ColorConversionCodes.BGR2GRAY);
            Cv2.Threshold(input, input, 100, 255, ThresholdTypes.BinaryInv);
            var scale = Math.Clamp(1855f / bounds.Height, .1f, 2f);
            Cv2.Resize(input, input, new Size(), scale, scale, InterpolationFlags.Linear);
            Cv2.Dilate(input, input, null, iterations: 1);
            using var pix = Pix.LoadFromMemory(input.ToBytes());
            pix.XRes = pix.YRes = 300;
            using var page = engine.Process(pix, PageSegMode.SingleBlock);
            return provider.IsValidClearHeader(page.GetText().Trim());
        }
        catch { return false; }
    }

    static OpenCvSharp.Rect DetectBounds(Mat color, bool relaxed)
    {
        using var gray = new Mat(); using var binary = new Mat(); using var edges = new Mat();
        Cv2.CvtColor(color, gray, ColorConversionCodes.BGR2GRAY);
        Cv2.Threshold(gray, binary, 100, 255, ThresholdTypes.BinaryInv);
        if (relaxed) { using var bright = new Mat(); Cv2.Threshold(gray, bright, 180, 255, ThresholdTypes.Binary); Cv2.BitwiseOr(binary, bright, binary); }
        Cv2.Canny(binary, edges, 150, 200, 3, false);
        var lines = Cv2.HoughLinesP(edges, 1, Math.PI / 180, relaxed ? Math.Min(500, (int)(color.Width * .25)) : 500, color.Width * .35, 0);
        int left = color.Width / 2, right = left, top = color.Height / 2, bottom = top;
        foreach (var line in lines)
        {
            int dx = line.P2.X - line.P1.X, dy = line.P2.Y - line.P1.Y;
            if (relaxed ? Math.Abs(dx) > Math.Abs(dy) && Math.Abs(dy) <= Math.Max(2, (int)(Math.Abs(dx) * .02)) : dy == 0)
            { top = Math.Min(top, Math.Min(line.P1.Y, line.P2.Y)); bottom = Math.Max(bottom, Math.Max(line.P1.Y, line.P2.Y)); }
            else if (relaxed ? Math.Abs(dy) > Math.Abs(dx) && Math.Abs(dx) <= Math.Max(2, (int)(Math.Abs(dy) * .02)) : dx == 0)
            { left = Math.Min(left, Math.Min(line.P1.X, line.P2.X)); right = Math.Max(right, Math.Max(line.P1.X, line.P2.X)); }
        }
        return OpenCvSharp.Rect.FromLTRB(left, top, right, bottom);
    }

}
