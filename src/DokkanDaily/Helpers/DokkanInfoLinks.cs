using System.Text.Json;

namespace DokkanDaily.Helpers;

public static class DokkanInfoLinks
{
    private static readonly Lazy<Dictionary<string, string>> StageLinks = new(() =>
    {
        using var stream = typeof(DokkanInfoLinks).Assembly.GetManifestResourceStream(
            "DokkanDaily.wwwroot.data.DokkanStageLinks.json");
        return JsonSerializer.Deserialize<Dictionary<string, string>>(stream!);
    });

    // Generated at content-update time; page requests never contact Dokkan Info.
    public static string ForStage(string fullName) => StageLinks.Value.GetValueOrDefault(fullName);
}
