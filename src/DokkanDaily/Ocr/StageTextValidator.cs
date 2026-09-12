using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace DokkanDaily.Ocr;

public sealed record StageTitleAlias(string EventTitle, string StageTitle, string Provenance = null);
public sealed record StageTextContrast(IEnumerable<string> Events, IEnumerable<string> Stages);
public sealed record StageTextTarget
{
    public int? EventId { get; init; }
    public int StageNumber { get; init; }
    public string MinimumDifficulty { get; init; }
    public IEnumerable<StageTitleAlias> Aliases { get; init; } = [];
    public IEnumerable<string> EventNames { get; init; }
    public StageTextContrast Contrast { get; init; }
}
public sealed record StageTextObservation(string EventTitle, string StageTitle, string Difficulty, bool ApplicationAccepted = true);
public sealed record StageTextResult(string Outcome, string Reason)
{
    public bool IsMismatch => Outcome is "event-mismatch" or "stage-mismatch" or "difficulty-mismatch";
}

/// <summary>Frozen target-aware tolerant policy. Inputs are independently observed text;
/// IDs only retrieve names. A match is an accidental-upload check, not proof of completion.</summary>
public static partial class StageTextValidator
{
    private static readonly string[] Difficulties = ["NORMAL", "HARD", "Z-HARD", "SUPER", "SUPER2", "SUPER3"];
    // Python's frozen Unicode casefold differs from invariant lowercase (e.g. ß, ς).
    // This static Unicode table has no OCR/model-specific substitutions.
    private static readonly Dictionary<string, string> CaseFold = LoadCaseFold();
    private static Dictionary<string, string> LoadCaseFold()
    {
        using Stream stream = typeof(StageTextValidator).Assembly.GetManifestResourceStream("DokkanDaily.Ocr.UnicodeCaseFold.json");
        return JsonSerializer.Deserialize<Dictionary<string, string>>(stream);
    }

    public static string Normalize(string text)
    {
        if (text is null)
        {
            return "";
        }
        StringBuilder folded = new StringBuilder();
        foreach (Rune rune in text.Normalize(NormalizationForm.FormKC).EnumerateRunes())
        {
            string character = rune.ToString();
            folded.Append(CaseFold.GetValueOrDefault(character, character));
        }
        return string.Concat(folded.ToString().EnumerateRunes().Where(r => Rune.IsLetter(r) || Rune.IsNumber(r)));
    }

    private static int[] Points(string text) => text.EnumerateRunes().Select(r => r.Value).ToArray();
    private static IEnumerable<string> Numbers(string text) => DigitRegex().Matches(Normalize(text)).Select(match => match.Value);
    private sealed record Evidence(int Distance, double RelativeDistance, bool WithinBudget, bool NumericAgreement);
    private static int[,] Alignment(int[] targetPoints, int[] observedPoints)
    {
        int[,] distances = new int[targetPoints.Length + 1, observedPoints.Length + 1];
        for (int targetIndex = 0; targetIndex <= targetPoints.Length; targetIndex++)
        {
            distances[targetIndex, 0] = targetIndex;
        }
        for (int observedIndex = 0; observedIndex <= observedPoints.Length; observedIndex++)
        {
            distances[0, observedIndex] = observedIndex;
        }

        for (int targetIndex = 1; targetIndex <= targetPoints.Length; targetIndex++)
        {
            for (int observedIndex = 1; observedIndex <= observedPoints.Length; observedIndex++)
            {
                int deletion = distances[targetIndex - 1, observedIndex] + 1;
                int insertion = distances[targetIndex, observedIndex - 1] + 1;
                int substitutionCost = targetPoints[targetIndex - 1] == observedPoints[observedIndex - 1] ? 0 : 1;
                int substitution = distances[targetIndex - 1, observedIndex - 1] + substitutionCost;
                distances[targetIndex, observedIndex] = Math.Min(Math.Min(deletion, insertion), substitution);
            }
        }
        return distances;
    }

    private static Evidence Score(string observed, string target)
    {
        int[] observedPoints = Points(Normalize(observed));
        int[] targetPoints = Points(Normalize(target));
        int distance = Alignment(observedPoints, targetPoints)[observedPoints.Length, targetPoints.Length];
        int length = Math.Max(observedPoints.Length, targetPoints.Length);
        int budget = length < 5 ? 0 : Math.Max(1, Math.Min(3, (int)(length * .16)));
        bool numericAgreement = Numbers(observed).SequenceEqual(Numbers(target));
        return new(distance, (double)distance / length, distance <= budget, numericAgreement);
    }

    private static int[] CountEditsByTargetPosition(int[] targetPoints, int[] observedPoints)
    {
        int[,] distances = Alignment(targetPoints, observedPoints);
        int[] edits = new int[targetPoints.Length];
        int targetIndex = targetPoints.Length;
        int observedIndex = observedPoints.Length;
        while (targetIndex > 0 || observedIndex > 0)
        {
            // Preserve the traceback preference: diagonal, deletion, then insertion.
            if (targetIndex > 0 && observedIndex > 0 &&
                distances[targetIndex, observedIndex] == distances[targetIndex - 1, observedIndex - 1] +
                    (targetPoints[targetIndex - 1] == observedPoints[observedIndex - 1] ? 0 : 1))
            {
                edits[targetIndex - 1] += targetPoints[targetIndex - 1] == observedPoints[observedIndex - 1] ? 0 : 1;
                targetIndex--;
                observedIndex--;
            }
            else if (targetIndex > 0 && distances[targetIndex, observedIndex] == distances[targetIndex - 1, observedIndex] + 1)
            {
                edits[targetIndex - 1]++;
                targetIndex--;
            }
            else
            {
                edits[Math.Min(targetIndex, targetPoints.Length - 1)]++;
                observedIndex--;
            }
        }
        return edits;
    }

    private static bool TokenConflicts(string observed, string target)
    {
        string foldedTarget = string.Concat(target.Normalize(NormalizationForm.FormKC).EnumerateRunes()
            .Select(rune => CaseFold.GetValueOrDefault(rune.ToString(), rune.ToString())));

        IEnumerable<Match> meaningfulWords = AlphabeticRegex().Matches(foldedTarget)
            .Where(word => word.Value is not ("a" or "an" or "the" or "of" or "vs"));

        int[] edits = null;
        foreach (Match word in meaningfulWords)
        {
            // Compute the alignment once, only if there is a meaningful word to check.
            edits ??= CountEditsByTargetPosition(Points(Normalize(target)), Points(Normalize(observed)));
            int startPosition = Normalize(foldedTarget[..word.Index]).EnumerateRunes().Count();
            int wordEdits = edits.Skip(startPosition).Take(word.Length).Sum();
            int wordBudget = word.Length <= 2 ? 0 : word.Length < 7 ? 1 : 2;
            if (wordEdits > wordBudget)
            {
                return true;
            }
        }
        return false;
    }

    private static StageTextResult Field(string observed, IEnumerable<string> names, IEnumerable<string> alternatives)
    {
        string normalizedObservation = Normalize(observed);
        // Reuse this snapshot: names may run event matching as they are enumerated.
        string[] targets = names.Where(t => Normalize(t).Length > 0).ToArray();
        if (normalizedObservation.Length == 0 || targets.Length == 0)
        {
            return new("unknown", "missing-text-or-target-alias");
        }
        HashSet<string> normalizedTargets = targets.Select(Normalize).ToHashSet();
        if (normalizedTargets.Contains(normalizedObservation))
        {
            return new("match", "normalized-exact");
        }

        (string Text, Evidence Evidence) closestTarget = targets
            .Select(text => (Text: text, Evidence: Score(observed, text)))
            .OrderBy(candidate => candidate.Evidence.RelativeDistance)
            .ThenBy(candidate => candidate.Evidence.Distance)
            .First();

        Evidence closestEvidence = closestTarget.Evidence;
        Evidence closestAlternative = alternatives
            .Where(text => Normalize(text).Length > 0 && !normalizedTargets.Contains(Normalize(text)))
            .Select(text => Score(observed, text))
            .Where(evidence => evidence.WithinBudget && evidence.NumericAgreement)
            .OrderBy(evidence => evidence.RelativeDistance)
            .ThenBy(evidence => evidence.Distance)
            .FirstOrDefault();

        if (closestAlternative is not null && closestAlternative.RelativeDistance < closestEvidence.RelativeDistance)
        {
            return new("mismatch", "closer-known-different-name");
        }
        if (closestAlternative is not null && closestAlternative.RelativeDistance == closestEvidence.RelativeDistance)
        {
            return new("unknown", "indistinguishable-ocr-alternatives");
        }
        if (!closestEvidence.NumericAgreement)
        {
            if (HasExplicitStageNumberConflict(observed, closestTarget.Text))
            {
                return new("mismatch", "explicit-stage-number-conflict");
            }
            return new("unknown", "numeric-token-not-established");
        }
        if (!closestEvidence.WithinBudget)
        {
            return new("unknown", "insufficient-text-agreement");
        }
        if (TokenConflicts(observed, closestTarget.Text))
        {
            return new("unknown", "name-or-qualifier-not-established");
        }
        return new("match", "bounded-ocr-error");
    }

    private static bool HasExplicitStageNumberConflict(string observed, string target)
    {
        Match observedNumber = ExplicitStageNumber(observed);
        Match targetNumber = ExplicitStageNumber(target);
        return observedNumber.Success && targetNumber.Success &&
            observedNumber.Groups[1].Value != targetNumber.Groups[1].Value &&
            DigitRegex().Replace(Normalize(observed), "") == DigitRegex().Replace(Normalize(target), "");
    }

    private static Match ExplicitStageNumber(string text) =>
        StageRegex().Match(text.Normalize(NormalizationForm.FormKC).ToLowerInvariant());

    public static StageTextTarget Prepare(StageTextTarget target, IReadOnlyList<StageTextTarget> catalog)
    {
        IEnumerable<StageTitleAlias> catalogAliases = catalog
            .SelectMany(entry => entry.Aliases);

        IEnumerable<StageTitleAlias> sameEventAliases = catalog
            .Where(entry => target.EventId.HasValue && entry.EventId == target.EventId)
            .SelectMany(entry => entry.Aliases);

        IEnumerable<string> eventNames = target.Aliases
            .Select(alias => alias.EventTitle)
            .Concat(sameEventAliases.Select(alias => alias.EventTitle))
            .Distinct()
            .Order(StringComparer.Ordinal);

        HashSet<string> normalizedEventNames = eventNames.Select(Normalize).ToHashSet();
        IEnumerable<string> contrastEvents = catalogAliases
            .Select(alias => alias.EventTitle)
            .Distinct()
            .Order(StringComparer.Ordinal);
        IEnumerable<string> contrastStages = catalogAliases
            .Where(alias => normalizedEventNames
                .Contains(Normalize(alias.EventTitle)))
            .Select(alias => alias.StageTitle)
            .Distinct()
            .Order(StringComparer.Ordinal);
        return target with
        {
            EventNames = eventNames,
            Contrast = new(contrastEvents, contrastStages)
        };
    }

    public static StageTextResult Validate(StageTextObservation observation, StageTextTarget target, IReadOnlyList<StageTextTarget> catalog = null)
    {
        if (observation is null || target is null)
        {
            return new("unknown", "invalid-input");
        }
        if (target.MinimumDifficulty is not null && !Difficulties.Contains(target.MinimumDifficulty))
        {
            return new("unknown", "invalid-required-difficulty");
        }
        int difficulty = Array.IndexOf(Difficulties, observation.Difficulty);
        if (difficulty < 0)
        {
            return new("unknown", "missing-or-unsupported-difficulty");
        }
        if (!observation.ApplicationAccepted)
        {
            return new("unknown", "application-layout-or-header-failed");
        }
        if (catalog is { Count: > 0 } && target.Contrast is null)
        {
            target = Prepare(target, catalog);
        }
        IEnumerable<StageTitleAlias> aliases = target.Aliases
            .Where(alias => Normalize(alias.EventTitle).Length > 0 && Normalize(alias.StageTitle).Length > 0);
        if (!aliases.Any())
        {
            return new("unknown", "missing-target-localized-names");
        }
        IEnumerable<string> events = target.EventNames ?? aliases.Select(a => a.EventTitle);
        StageTextResult eventResult = Field(observation.EventTitle, events, target.Contrast?.Events ?? []);
        if (eventResult.Outcome != "match")
        {
            return eventResult with { Outcome = eventResult.Outcome == "mismatch" ? "event-mismatch" : "unknown" };
        }
        IEnumerable<StageTitleAlias> matchingEventAliases = aliases.Where(a => Field(observation.EventTitle, [a.EventTitle], []).Outcome == "match");
        StageTextResult stageResult = Field(observation.StageTitle, matchingEventAliases.Select(a => a.StageTitle), target.Contrast?.Stages ?? []);
        if (stageResult.Outcome != "match")
        {
            return stageResult with { Outcome = stageResult.Outcome == "mismatch" ? "stage-mismatch" : "unknown" };
        }
        if (target.MinimumDifficulty is not null && difficulty < Array.IndexOf(Difficulties, target.MinimumDifficulty))
        {
            return new("difficulty-mismatch", "below-assigned-minimum");
        }
        return new("match", "visible-text-consistent-with-assignment");
    }

    [GeneratedRegex(@"\d+")]
    private static partial Regex DigitRegex();
    [GeneratedRegex(@"(?:stage|ステージ)\s*#?\s*(\d+)")]
    private static partial Regex StageRegex();
    [GeneratedRegex("[a-z]+")]
    private static partial Regex AlphabeticRegex();
}
