using SixLabors.ImageSharp;

namespace DokkanDaily.Ocr
{
    public class ClearScreenUI(int width, int height, string boundingBoxImagePath)
    {
        private readonly Dictionary<string, RegionLoader.RelativeRegion> Regions = RegionLoader.LoadUIRegions(boundingBoxImagePath);

        // The Latin difficulty label occupies the same strip in Global and JP.
        // Stop before the stars, which are not part of the difficulty string.
        public Rectangle GetDifficultyRegion() => new(
            (int)(width * 0.12f), (int)(height * 0.17f),
            (int)(width * 0.40f), (int)(height * 0.06f));
        public Rectangle GetStageClearDetailsRegion()
        {
            RegionLoader.RelativeRegion normalizedStageClearDetailsRegion = Regions["stageClearDetails"];
            return new Rectangle(
                (int)(normalizedStageClearDetailsRegion.X * width),
                (int)(normalizedStageClearDetailsRegion.Y * height),
                (int)(normalizedStageClearDetailsRegion.Width * width),
                (int)(normalizedStageClearDetailsRegion.Height * height)
            );
        }

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
