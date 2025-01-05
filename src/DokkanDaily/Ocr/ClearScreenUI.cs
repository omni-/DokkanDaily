using SixLabors.ImageSharp;

namespace DokkanDaily.Ocr
{
    public class ClearScreenUI(int width, int height)
    {
        private static readonly Dictionary<string, RegionLoader.RelativeRegion> Regions = RegionLoader.LoadUIRegions();
        public Rectangle GetNicknameRegion()
        {
            RegionLoader.RelativeRegion normalizedNicknameRegion = Regions["nickname"];
            return new Rectangle(
                (int)(normalizedNicknameRegion.X * width),
                (int)(normalizedNicknameRegion.Y * height),
                (int)(normalizedNicknameRegion.Width * width),
                (int)(normalizedNicknameRegion.Height * height)
            );
        }

        public Rectangle GetCleartimeRegion()
        {
            RegionLoader.RelativeRegion normalizedCleartimeRegion = Regions["cleartime"];
            return new Rectangle(
                (int)(normalizedCleartimeRegion.X * width),
                (int)(normalizedCleartimeRegion.Y * height),
                (int)(normalizedCleartimeRegion.Width * width),
                (int)(normalizedCleartimeRegion.Height * height)
            );
        }

        public Rectangle GetItemlessRegion()
        {
            RegionLoader.RelativeRegion normalizedItemlessRegion = Regions["itemless"];
            return new Rectangle(
                (int)(normalizedItemlessRegion.X * width),
                (int)(normalizedItemlessRegion.Y * height),
                (int)(normalizedItemlessRegion.Width * width),
                (int)(normalizedItemlessRegion.Height * height)
            );
        }
    }
}
