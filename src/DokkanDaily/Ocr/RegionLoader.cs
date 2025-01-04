using OpenCvSharp;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace DokkanDaily.Ocr;

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

internal static class RegionLoader
{
    public record RelativeRegion(float X, float Y, float Width, float Height);

    public static Dictionary<string, RelativeRegion> LoadUIRegions()
    {
        string regionMapPath = Path.Join(Directory.GetCurrentDirectory(), "Ocr/boxes.png");

        using Image<Rgba32> regionMap = Image.Load<Rgba32>(regionMapPath);

        Dictionary<string, Rgba32> knownRegionColors = new()
        {
            {
                "nickname", new Rgba32(255, 0, 0)
            },
            {
                "cleartime", new Rgba32(0, 255, 0)
            },
            {
                "itemless", new Rgba32(0, 0, 255)
            }
        };

        Dictionary<Rgba32, Rectangle> foundRegions = DetectRegionsByColor(regionMap);

        Rectangle? nicknameRegion = foundRegions[knownRegionColors["nickname"]];
        Rectangle? cleartimeRegion = foundRegions[knownRegionColors["cleartime"]];
        Rectangle? itemlessRegion = foundRegions[knownRegionColors["itemless"]];
        if (nicknameRegion == null || cleartimeRegion == null || itemlessRegion == null)
        {
            throw new Exception("Failed to find all required regions in the region map.");
        }

        RelativeRegion normalizedNicknameRegion = new (
            nicknameRegion.Value.Location.X / (float) regionMap.Size.Width,
            nicknameRegion.Value.Location.Y / (float) regionMap.Size.Height,
            nicknameRegion.Value.Width / (float) regionMap.Size.Width,
            nicknameRegion.Value.Height / (float) regionMap.Size.Height
        );

        RelativeRegion normalizedCleartimeRegion = new (
            cleartimeRegion.Value.Location.X / (float) regionMap.Size.Width,
            cleartimeRegion.Value.Location.Y / (float) regionMap.Size.Height,
            cleartimeRegion.Value.Width / (float) regionMap.Size.Width,
            cleartimeRegion.Value.Height / (float) regionMap.Size.Height
        );

        RelativeRegion normalizedItemlessRegion = new (
            itemlessRegion.Value.Location.X / (float) regionMap.Size.Width,
            itemlessRegion.Value.Location.Y / (float) regionMap.Size.Height,
            itemlessRegion.Value.Width / (float) regionMap.Size.Width,
            itemlessRegion.Value.Height / (float) regionMap.Size.Height
        );

        return new Dictionary<string, RelativeRegion>
        {
            {
                "nickname", normalizedNicknameRegion
            },
            {
                "cleartime", normalizedCleartimeRegion
            },
            {
                "itemless", normalizedItemlessRegion
            }
        };
    }

    // Detect rectangles for unique colors in the region map
    static Dictionary<Rgba32, Rectangle> DetectRegionsByColor(Image<Rgba32> map)
    {
        var regions = new Dictionary<Rgba32, Rectangle>();

        for (int y = 0; y < map.Height; y++)
        {
            for (int x = 0; x < map.Width; x++)
            {
                Rgba32 color = map[x, y];

                // Ignore fully transparent pixels and already processed pixels
                if (color.A == 0)
                {
                    continue;
                }

                if (!regions.ContainsKey(color))
                {
                    Rectangle rect = TraceBoundingRectangle(map, color, x, y);
                    regions[color] = rect;
                }
            }
        }

        return regions;
    }

    static Rectangle TraceBoundingRectangle(Image<Rgba32> map, Rgba32 color, int startX, int startY)
    {
        int minX = startX, minY = startY, maxX = startX, maxY = startY;

        // Move right to find the maximum X boundary
        for (int x = startX; x < map.Width && map[x, startY] == color; x++)
        {
            maxX = x;
        }

        // Move down to find the maximum Y boundary
        for (int y = startY; y < map.Height && map[startX, y] == color; y++)
        {
            maxY = y;
        }

        return new Rectangle(minX, minY, maxX - minX + 1, maxY - minY + 1);
    }
}