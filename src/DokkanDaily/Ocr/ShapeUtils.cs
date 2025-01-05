using OpenCvSharp;

namespace DokkanDaily.Ocr;

public static class ShapeUtils
{
    public static bool IsValidRectangle(Point[] contour, double minimum)
    {
        if (contour.Length != 4) return false;
        double side1 = GetLength(contour[0], contour[1]);
        double side2 = GetLength(contour[1], contour[2]);
        double side3 = GetLength(contour[2], contour[3]);
        double side4 = GetLength(contour[3], contour[0]);

        if (Math.Abs(side1 - side3) / Math.Max(side1, side3) > minimum) return false;
        if (Math.Abs(side2 - side4) / Math.Max(side2, side4) > minimum) return false;

        return true;

        double GetLength(Point p1, Point p2) => Math.Abs(p1.X - p2.X) + Math.Abs(p1.Y - p2.Y);
    }

    public static void PreviewImage(String title, Mat image, int delay = 0)
    {
        Size screenResolution = new(2560, 1440);

        Size imageSize = image.Size();
        if (imageSize.Height > screenResolution.Height)
        {
            image = image.Clone(); // so we don't modify the image that was passed in
            // scale down the image if it's bigger than the screen height
            double scalingFactor = screenResolution.Height / (double)imageSize.Height;
            scalingFactor *= 0.9; // make it a little smaller than the exact screen height
            Cv2.Resize(image, image, new Size(), scalingFactor, scalingFactor);
        }

        Cv2.ImShow(title, image);
        Cv2.MoveWindow(title, screenResolution.Width / 2 - image.Size().Width / 2, 0);
        Cv2.WaitKey(delay);
        Cv2.DestroyWindow(title);
    }
}
