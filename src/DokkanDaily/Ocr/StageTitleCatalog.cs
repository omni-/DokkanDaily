using System.Text.Json;
using System.Text.RegularExpressions;
using DokkanDaily.Models;

namespace DokkanDaily.Ocr;

// Localized names enrich the existing Stage/DokkanStageLinks data, not a second
// challenge pool. Historical neighbors are retained solely for text contrast.
public static class StageTitleCatalog
{
    private static readonly Lazy<Dictionary<(int?, int), StageTextTarget>> Targets = new(() =>
    {
        using var stream = typeof(StageTitleCatalog).Assembly.GetManifestResourceStream("DokkanDaily.Ocr.StageTitleAliases.json");
        var rows = JsonSerializer.Deserialize<StageTextTarget[]>(stream, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        return rows.ToDictionary(t => (t.EventId, t.StageNumber), t => StageTextValidator.Prepare(t, rows));
    });

    public static StageTextTarget ForStage(Stage stage)
    {
        var id = Regex.Match(stage.DokkanInfoUrl ?? "", @"/challenge/(\d+)(?:/|$)");
        var target = id.Success && Targets.Value.TryGetValue((int.Parse(id.Groups[1].Value), stage.StageNumber), out var known)
            ? known : new StageTextTarget();
        return target with { MinimumDifficulty = stage.MinimumDifficulty == Models.Enums.StageDifficulty.ZHARD ? "Z-HARD" : stage.MinimumDifficulty?.ToString() };
    }
}
