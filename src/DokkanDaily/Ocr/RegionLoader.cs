﻿using DokkanDaily.Exceptions;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace DokkanDaily.Ocr;

internal static class RegionLoader
{
    public record RelativeRegion(float X, float Y, float Width, float Height);

    private static readonly Dictionary<string, Rgba32> knownRegionColors = new()
    {
        {
            "stageClearDetails", new Rgba32(255, 255, 0)
        },
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

    private static readonly Dictionary<string, Dictionary<string, RelativeRegion>> cachedMaps = [];

    public static Dictionary<string, RelativeRegion> LoadUIRegions(string regionMapPath)
    {
        if (cachedMaps.TryGetValue(regionMapPath, out Dictionary<string, RelativeRegion> value))
            return value;

        using Image<Rgba32> regionMap = Image.Load<Rgba32>(regionMapPath);

        Dictionary<Rgba32, Rectangle> foundRegions = DetectRegionsByColor(regionMap);

        Rectangle? stageClearDetailsRegion = foundRegions[knownRegionColors["stageClearDetails"]];
        Rectangle? nicknameRegion = foundRegions[knownRegionColors["nickname"]];
        Rectangle? cleartimeRegion = foundRegions[knownRegionColors["cleartime"]];
        Rectangle? itemlessRegion = foundRegions[knownRegionColors["itemless"]];
        if (nicknameRegion == null || cleartimeRegion == null || itemlessRegion == null)
        {
            throw new OcrServiceException("Failed to find all required regions in the region map.");
        }

        RelativeRegion normalizedStageClearDetailsRegion = new(
            stageClearDetailsRegion.Value.Location.X / (float)regionMap.Size.Width,
            stageClearDetailsRegion.Value.Location.Y / (float)regionMap.Size.Height,
            stageClearDetailsRegion.Value.Width / (float)regionMap.Size.Width,
            stageClearDetailsRegion.Value.Height / (float)regionMap.Size.Height
        );

        RelativeRegion normalizedNicknameRegion = new(
            nicknameRegion.Value.Location.X / (float)regionMap.Size.Width,
            nicknameRegion.Value.Location.Y / (float)regionMap.Size.Height,
            nicknameRegion.Value.Width / (float)regionMap.Size.Width,
            nicknameRegion.Value.Height / (float)regionMap.Size.Height
        );

        RelativeRegion normalizedCleartimeRegion = new(
            cleartimeRegion.Value.Location.X / (float)regionMap.Size.Width,
            cleartimeRegion.Value.Location.Y / (float)regionMap.Size.Height,
            cleartimeRegion.Value.Width / (float)regionMap.Size.Width,
            cleartimeRegion.Value.Height / (float)regionMap.Size.Height
        );

        RelativeRegion normalizedItemlessRegion = new(
            itemlessRegion.Value.Location.X / (float)regionMap.Size.Width,
            itemlessRegion.Value.Location.Y / (float)regionMap.Size.Height,
            itemlessRegion.Value.Width / (float)regionMap.Size.Width,
            itemlessRegion.Value.Height / (float)regionMap.Size.Height
        );

        var map = new Dictionary<string, RelativeRegion>
        {
            { "stageClearDetails", normalizedStageClearDetailsRegion },
            { "nickname", normalizedNicknameRegion },
            { "cleartime", normalizedCleartimeRegion },
            { "itemless", normalizedItemlessRegion }
        };

        cachedMaps[regionMapPath] = map;

        return map;
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