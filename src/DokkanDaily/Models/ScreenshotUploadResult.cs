using Azure.Storage.Blobs;
using DokkanDaily.Ocr;

namespace DokkanDaily.Models;

public sealed record ScreenshotUploadResult(BlobClient Blob, StageTextResult Validation);
