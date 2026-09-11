using DokkanDaily.Exceptions;
using DokkanDaily.Ocr;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace DokkanDailyTests;

public class RegionLoaderTests
{
    [Test]
    public async Task ConcurrentLoadsReturnCompleteRegions()
    {
        string path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".png");
        try
        {
            using (var image = new Image<Rgba32>(4, 1))
            {
                image[0, 0] = new(255, 255, 0); image[1, 0] = new(255, 0, 0);
                image[2, 0] = new(0, 255, 0); image[3, 0] = new(0, 0, 255);
                image.SaveAsPng(path);
            }
            var results = await Task.WhenAll(Enumerable.Range(0, 50).Select(_ => Task.Run(() => RegionLoader.LoadUIRegions(path))));
            Assert.That(results.All(x => x.Count == 4), Is.True);
        }
        finally { File.Delete(path); }
    }

    [Test]
    public void MissingStageDetailsReportsOcrError()
    {
        string path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".png");
        try
        {
            using (var image = new Image<Rgba32>(3, 1))
            {
                image[0, 0] = new(255, 0, 0); image[1, 0] = new(0, 255, 0); image[2, 0] = new(0, 0, 255);
                image.SaveAsPng(path);
            }
            Assert.Throws<OcrServiceException>(() => RegionLoader.LoadUIRegions(path));
        }
        finally { File.Delete(path); }
    }
}
