using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace DokkanDaily.Ocr;

public sealed record StageTitleAlias(string EventTitle, string StageTitle, string Provenance = null);
public sealed record StageTextContrast(string[] Events, string[] Stages);
public sealed record StageTextTarget
{
    public int? EventId { get; init; }
    public int StageNumber { get; init; }
    public string MinimumDifficulty { get; init; }
    public StageTitleAlias[] Aliases { get; init; } = [];
    public string[] EventNames { get; init; }
    public StageTextContrast Contrast { get; init; }
}
public sealed record StageTextObservation(string EventTitle, string StageTitle, string Difficulty, bool ApplicationAccepted = true);
public sealed record StageTextResult(string Outcome, string Reason)
{
    public bool IsMismatch => Outcome is "event-mismatch" or "stage-mismatch" or "difficulty-mismatch";
}

/// <summary>Frozen target-aware tolerant policy. Inputs are independently observed text;
/// IDs only retrieve names. A match is an accidental-upload check, not proof of completion.</summary>
public static class StageTextValidator
{
    private static readonly string[] Difficulties = ["NORMAL", "HARD", "Z-HARD", "SUPER", "SUPER2", "SUPER3"];
    // Python's frozen Unicode casefold differs from invariant lowercase (e.g. ß, ς).
    // This static Unicode table has no OCR/model-specific substitutions.
    private static readonly Dictionary<string, string> CaseFold = LoadCaseFold();
    private static Dictionary<string, string> LoadCaseFold()
    {
        using var stream = typeof(StageTextValidator).Assembly.GetManifestResourceStream("DokkanDaily.Ocr.UnicodeCaseFold.json");
        return JsonSerializer.Deserialize<Dictionary<string, string>>(stream);
    }

    public static string Normalize(string text)
    {
        if (text is null) return "";
        var folded = new StringBuilder();
        foreach (var rune in text.Normalize(NormalizationForm.FormKC).EnumerateRunes())
            folded.Append(CaseFold.GetValueOrDefault(rune.ToString(), rune.ToString()));
        return string.Concat(folded.ToString().EnumerateRunes().Where(r => Rune.IsLetter(r) || Rune.IsNumber(r)));
    }

    private static int[] Points(string text) => text.EnumerateRunes().Select(r => r.Value).ToArray();
    private static string[] Numbers(string text) => Regex.Matches(Normalize(text), @"\d+").Select(m => m.Value).ToArray();
    private sealed record Evidence(int Distance, double RelativeDistance, bool WithinBudget, bool NumericAgreement);
    private static int[,] Alignment(int[] a, int[] b)
    {
        var dp = new int[a.Length + 1, b.Length + 1];
        for (int i = 0; i <= a.Length; i++) dp[i, 0] = i;
        for (int j = 0; j <= b.Length; j++) dp[0, j] = j;
        for (int i = 1; i <= a.Length; i++)
            for (int j = 1; j <= b.Length; j++)
                dp[i, j] = Math.Min(Math.Min(dp[i - 1, j] + 1, dp[i, j - 1] + 1), dp[i - 1, j - 1] + (a[i - 1] == b[j - 1] ? 0 : 1));
        return dp;
    }
    private static Evidence Score(string observed, string target)
    {
        var a = Points(Normalize(observed)); var b = Points(Normalize(target));
        int distance = Alignment(a, b)[a.Length, b.Length], length = Math.Max(a.Length, b.Length);
        int budget = length < 5 ? 0 : Math.Max(1, Math.Min(3, (int)(length * .16)));
        return new(distance, (double)distance / length, distance <= budget, Numbers(observed).SequenceEqual(Numbers(target)));
    }
    private static bool TokenConflicts(string observed, string target)
    {
        var a = Points(Normalize(target)); var b = Points(Normalize(observed));
        string raw = string.Concat(target.Normalize(NormalizationForm.FormKC).EnumerateRunes().Select(r => CaseFold.GetValueOrDefault(r.ToString(), r.ToString())));
        var words = Regex.Matches(raw, "[a-z]+").Where(m => m.Value is not ("a" or "an" or "the" or "of" or "vs")).ToArray();
        if (words.Length == 0) return false;
        var dp = Alignment(a, b); var edits = new int[a.Length];
        int i = a.Length, j = b.Length;
        while (i > 0 || j > 0)
        {
            if (i > 0 && j > 0 && dp[i, j] == dp[i - 1, j - 1] + (a[i - 1] == b[j - 1] ? 0 : 1))
            { edits[i - 1] += a[i - 1] == b[j - 1] ? 0 : 1; i--; j--; }
            else if (i > 0 && dp[i, j] == dp[i - 1, j] + 1) { edits[i - 1]++; i--; }
            else { edits[Math.Min(i, a.Length - 1)]++; j--; }
        }
        return words.Any(m => edits.Skip(Points(Normalize(raw[..m.Index])).Length).Take(m.Length).Sum() > (m.Length <= 2 ? 0 : m.Length < 7 ? 1 : 2));
    }

    private static StageTextResult Field(string observed, IEnumerable<string> names, IEnumerable<string> alternatives)
    {
        string obs = Normalize(observed);
        var targets = names.Where(t => Normalize(t).Length > 0).ToArray();
        if (obs.Length == 0 || targets.Length == 0) return new("unknown", "missing-text-or-target-alias");
        var norms = targets.Select(Normalize).ToHashSet();
        if (norms.Contains(obs)) return new("match", "normalized-exact");
        var scored = targets.Select(t => (Text: t, Evidence: Score(observed, t))).OrderBy(x => x.Evidence.RelativeDistance).ThenBy(x => x.Evidence.Distance).First();
        var best = scored.Evidence;
        var rival = alternatives.Where(t => Normalize(t).Length > 0 && !norms.Contains(Normalize(t)))
            .Select(t => Score(observed, t)).Where(e => e.WithinBudget && e.NumericAgreement)
            .OrderBy(e => e.RelativeDistance).ThenBy(e => e.Distance).FirstOrDefault();
        if (rival is not null && rival.RelativeDistance < best.RelativeDistance) return new("mismatch", "closer-known-different-name");
        if (rival is not null && rival.RelativeDistance == best.RelativeDistance) return new("unknown", "indistinguishable-ocr-alternatives");
        if (!best.NumericAgreement)
        {
            Match Explicit(string s) => Regex.Match(s.Normalize(NormalizationForm.FormKC).ToLowerInvariant(), @"(?:stage|ステージ)\s*#?\s*(\d+)");
            var on = Explicit(observed); var tn = Explicit(scored.Text);
            if (on.Success && tn.Success && on.Groups[1].Value != tn.Groups[1].Value &&
                Regex.Replace(obs, @"\d+", "") == Regex.Replace(Normalize(scored.Text), @"\d+", ""))
                return new("mismatch", "explicit-stage-number-conflict");
            return new("unknown", "numeric-token-not-established");
        }
        if (!best.WithinBudget) return new("unknown", "insufficient-text-agreement");
        return TokenConflicts(observed, scored.Text) ? new("unknown", "name-or-qualifier-not-established") : new("match", "bounded-ocr-error");
    }

    public static StageTextTarget Prepare(StageTextTarget target, IReadOnlyList<StageTextTarget> catalog)
    {
        var aliases = catalog.SelectMany(t => t.Aliases).ToArray();
        var events = target.Aliases.Select(a => a.EventTitle).Concat(catalog.Where(t => target.EventId.HasValue && t.EventId == target.EventId).SelectMany(t => t.Aliases).Select(a => a.EventTitle)).Distinct().Order(StringComparer.Ordinal).ToArray();
        var family = events.Select(Normalize).ToHashSet();
        return target with { EventNames = events, Contrast = new(aliases.Select(a => a.EventTitle).Distinct().Order(StringComparer.Ordinal).ToArray(), aliases.Where(a => family.Contains(Normalize(a.EventTitle))).Select(a => a.StageTitle).Distinct().Order(StringComparer.Ordinal).ToArray()) };
    }

    public static StageTextResult Validate(StageTextObservation observation, StageTextTarget target, IReadOnlyList<StageTextTarget> catalog = null)
    {
        if (observation is null || target is null) return new("unknown", "invalid-input");
        if (target.MinimumDifficulty is not null && !Difficulties.Contains(target.MinimumDifficulty)) return new("unknown", "invalid-required-difficulty");
        int difficulty = Array.IndexOf(Difficulties, observation.Difficulty);
        if (difficulty < 0) return new("unknown", "missing-or-unsupported-difficulty");
        if (!observation.ApplicationAccepted) return new("unknown", "application-layout-or-header-failed");
        if (catalog is { Count: > 0 } && target.Contrast is null) target = Prepare(target, catalog);
        var aliases = target.Aliases.Where(a => Normalize(a.EventTitle).Length > 0 && Normalize(a.StageTitle).Length > 0).ToArray();
        if (aliases.Length == 0) return new("unknown", "missing-target-localized-names");
        var events = target.EventNames ?? aliases.Select(a => a.EventTitle).ToArray();
        var eventResult = Field(observation.EventTitle, events, target.Contrast?.Events ?? []);
        if (eventResult.Outcome != "match") return eventResult with { Outcome = eventResult.Outcome == "mismatch" ? "event-mismatch" : "unknown" };
        var paired = aliases.Where(a => Field(observation.EventTitle, [a.EventTitle], []).Outcome == "match");
        var stageResult = Field(observation.StageTitle, paired.Select(a => a.StageTitle), target.Contrast?.Stages ?? []);
        if (stageResult.Outcome != "match") return stageResult with { Outcome = stageResult.Outcome == "mismatch" ? "stage-mismatch" : "unknown" };
        if (target.MinimumDifficulty is not null && difficulty < Array.IndexOf(Difficulties, target.MinimumDifficulty)) return new("difficulty-mismatch", "below-assigned-minimum");
        return new("match", "visible-text-consistent-with-assignment");
    }
}
