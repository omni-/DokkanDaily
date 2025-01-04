using OpenCvSharp;
using SixLabors.ImageSharp;

namespace DokkanDaily.Ocr;

public static class Extensions
{
    public static Rect ToCv2Rect(this Rectangle rect) => new(rect.X, rect.Y, rect.Width, rect.Height);
}