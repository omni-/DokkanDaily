using System.Security.Cryptography;
using System.Text.Json;
using DokkanDaily.Configuration;
using DokkanDaily.Constants;
using DokkanDaily.Models;
using DokkanDaily.Ocr;
using DokkanDaily.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

// Execute with the production image's deps/runtimeconfig; mount only this DLL and fixtures.
var json = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
string Hash(string path) => Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(path)));
var model = "wwwroot/tessdata/jpn-stage-v2.traineddata";
if (Hash(model) != "0bf65e5fe79140725174ca41d4f1a193c8f2232224e9df31dba657cf928fdf5e")
    throw new InvalidDataException("Packaged model differs from the frozen candidate");
using var fixture = JsonDocument.Parse(File.ReadAllText(Path.Combine(args[0], "runtime-regression.json")));
foreach (var row in fixture.RootElement.EnumerateArray())
{
    string path = Path.Combine(args[0], Path.GetFileName(row.GetProperty("sourcePath").GetString()));
    if (Hash(path) != row.GetProperty("sourceSha256").GetString()) throw new InvalidDataException("Fixture hash mismatch");
    var service = new OcrService(NullLogger<OcrService>.Instance,
        Options.Create(new DokkanDailySettings { FeatureFlags = new() { EnableJapaneseParsing = true } }), new());
    using var stream = new MemoryStream(File.ReadAllBytes(path));
    var actual = service.ProcessImage(stream);
    var expected = row.GetProperty("linuxMetadata").Deserialize<ClearMetadata>(json);
    if (actual is null || JsonSerializer.Serialize(actual) != JsonSerializer.Serialize(expected))
        throw new InvalidDataException($"Production OCR differs for {row.GetProperty("id")}: {JsonSerializer.Serialize(actual)}");
    Console.WriteLine($"Production native OCR passed: {row.GetProperty("id")}");
}
var stage = DokkanConstants.Stages.First(s => s.Name == "Fearsome Activation! Cell Max" && s.StageNumber == 2);
var target = StageTitleCatalog.ForStage(stage);
var match = StageTextValidator.Validate(new(stage.Name, "Horrendous Calamity", "SUPER3"), target);
var wrong = StageTextValidator.Validate(new(stage.Name, stage.Name, "SUPER3"), target);
var unknown = StageTextValidator.Validate(new(null, null, "SUPER3"), target);
if (match.Outcome != "match" || wrong.Outcome != "stage-mismatch" || unknown.Outcome != "unknown")
    throw new InvalidDataException("Packaged catalog/matcher failed conservative outcomes");
Console.WriteLine("Packaged model, catalog, case-fold resource and conservative matcher passed.");
