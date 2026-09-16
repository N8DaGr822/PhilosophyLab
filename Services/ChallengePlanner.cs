using PhilosophyTester.Models;

namespace PhilosophyTester.Services;

/// <summary>
/// Builds a personal challenge set: situations that call for the opposite of how you usually work.
/// </summary>
public sealed class ChallengePlanner(DeviceStore store, BehaviorAnalyzer analyzer)
{
    public const int SetSize = 6;

    public sealed record Plan(TestDefinition Test, bool Personalized);

    public async Task<Plan> BuildAsync(TestDefinition source, string? personId = null)
    {
        var ownerId = personId ?? (await store.GetActivePersonAsync()).Id;
        var results = await store.GetLatestResultsAsync(ownerId);
        var baseline = BehaviorAnalyzer.Baseline(await analyzer.ObserveAsync(results));

        // Recently seen challenge questions go to the back of the line.
        var lastRun = (await store.GetResultsAsync(ownerId)).FirstOrDefault(r => r.TestId == source.Id);
        bool Seen(Question q) => lastRun?.For(q.Id) is not null;

        double Opposition(Question q) => q.Demands
            .Select(kv => baseline.TryGetValue(kv.Key, out var t) && t.Weight >= 2 ? -Math.Sign(kv.Value) * t.Score : 0)
            .DefaultIfEmpty(0).Max();

        var ranked = source.Questions
            .Select(q => (Q: q, Score: Opposition(q)))
            .OrderByDescending(x => x.Score >= BehaviorAnalyzer.ChallengeThreshold)
            .ThenBy(x => Seen(x.Q))
            .ThenByDescending(x => x.Score)
            .ToList();

        bool personalized = ranked.Any(x => x.Score >= BehaviorAnalyzer.ChallengeThreshold);
        var chosen = ranked.Take(SetSize).Select(x => x.Q).ToHashSet();

        var test = new TestDefinition
        {
            Id = source.Id,
            Version = source.Version,
            Lab = source.Lab,
            Dynamic = true,
            Title = source.Title,
            Category = source.Category,
            Icon = source.Icon,
            Summary = source.Summary,
            Intro = source.Intro,
            MinutesEstimate = source.MinutesEstimate,
            // Keep the file's order so the set reads naturally.
            Questions = source.Questions.Where(chosen.Contains).ToList(),
        };
        return new Plan(test, personalized);
    }
}
