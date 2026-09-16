using PhilosophyTester.Models;

namespace PhilosophyTester.Services;

public sealed record AxisReading(Dimension Dimension, double Score, double Weight);

public sealed record PhilosophicalProfile(
    IReadOnlyList<AxisReading> Axes,
    IReadOnlyList<string> Observations,
    IReadOnlyList<Tension> Tensions,
    int TestsCompleted);

public sealed class ProfileBuilder(TestCatalog catalog, IEnumerable<IConsistencyRule> rules)
{
    private const double Leaning = 0.2;
    private const double Strong = 0.55;

    /// <summary>Beliefs-lab profile. Behavior results are ignored except by cross-lab rules.</summary>
    public async Task<PhilosophicalProfile> BuildAsync(IReadOnlyList<TestResult> latestResults)
    {
        var dims = await catalog.GetDimensionsAsync(Labs.Philosophy);
        var philosophyIds = (await catalog.GetTestsAsync(Labs.Philosophy)).Select(t => t.Id).ToHashSet();
        var mine = latestResults.Where(r => philosophyIds.Contains(r.TestId)).ToList();
        var combined = Scoring.Combine(mine.Select(r => r.Tallies));

        var axes = dims
            .Where(d => combined.ContainsKey(d.Id))
            .Select(d => new AxisReading(d, combined[d.Id].Score, combined[d.Id].Weight))
            .ToList();

        var tensions = await TensionsAsync(latestResults, Labs.Philosophy);
        return new PhilosophicalProfile(axes, Describe(axes), tensions, mine.Count);
    }

    /// <summary>Tensions for one lab, plus cross-lab ones ("what you believe vs. what you do").</summary>
    public Task<List<Tension>> TensionsAsync(IReadOnlyList<TestResult> latestResults, string lab)
    {
        var byTest = latestResults.ToDictionary(r => r.TestId);
        return Task.FromResult(rules
            .SelectMany(rule => rule.Evaluate(byTest))
            .Where(t => t.Lab == lab || t.Lab == Labs.Cross)
            .ToList());
    }

    /// <summary>Plain-language sentences for the clearest leanings. Never a label or verdict.</summary>
    public static List<string> Describe(IEnumerable<AxisReading> axes)
    {
        var lines = new List<string>();
        foreach (var a in axes.OrderByDescending(a => Math.Abs(a.Score)))
        {
            double m = Math.Abs(a.Score);
            if (m < Leaning) continue;
            var summary = a.Score > 0 ? a.Dimension.HighSummary : a.Dimension.LowSummary;
            lines.Add(m >= Strong ? $"Consistently, {summary}" : $"Often, {summary}");
        }
        if (lines.Count == 0)
            lines.Add("Your answers sit close to the middle on every axis so far — you weigh each situation on its own terms.");
        return lines;
    }

    public static string Describe(double score, Dimension d) => Math.Abs(score) switch
    {
        < Leaning => "Balanced",
        < Strong => $"Leans {(score > 0 ? d.HighPole : d.LowPole).ToLowerInvariant()}",
        _ => $"Strongly {(score > 0 ? d.HighPole : d.LowPole).ToLowerInvariant()}"
    };
}
