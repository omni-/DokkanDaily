using DokkanDaily.Models.Enums;

namespace DokkanDaily.Models
{
    public class Event(string name, Tier d, string path, int stage = 1) : ITieredObject
    {
        public string Name { get; init; } = name;

        public string FullName => $"{Name}, Stage {Stage}";

        public Tier Tier { get; init; } = d;

        private readonly string folder = path;

        public string WallpaperImagePath => $"images/events/{folder}/wall.png";

        public string BannerImagePath => $"images/events/{folder}/banner.png";

        public int Stage { get; init; } = stage;
    }
}
