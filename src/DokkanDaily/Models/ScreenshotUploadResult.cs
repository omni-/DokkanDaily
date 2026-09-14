using Azure.Storage.Blobs;
using DokkanDaily.Ocr;

namespace DokkanDaily.Models;

public sealed record ScreenshotUploadResult(BlobClient Blob, StageTextResult Validation)
{
    public IReadOnlyList<string> RemovedClearNames { get; init; } = [];
    public bool ReplacementIncomplete { get; init; }
}
