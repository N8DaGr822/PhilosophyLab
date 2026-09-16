using PhilosophyTester.Models;

namespace PhilosophyTester.Services;

/// <summary>One answered question with what it contributed to each dimension.</summary>
public sealed record Observation(TestDefinition Test, Question Question, Dictionary<string, double> Values, DateTimeOffset At);

/// <summary>"You get more directive when: time is short; others are new to the work."</summary>
public sealed record SituationalTendency(Dimension Dimension, string Shift, IReadOnlyList<string> Whens);
public sealed record ChoiceEvidence(string ResultId, string TestTitle, string Question, string Answer, string? Context);
public sealed record StrategyEvidence(string Strategy, int Chosen, int Opportunities, IReadOnlyList<ChoiceEvidence> Examples);

/// <summary>A situation that called for a particular style.</summary>
public sealed record AdaptMoment(Observation Observation, Dimension Dimension, double Baseline, bool IsChallenge, bool Adapted);

public sealed record AdaptabilitySummary(IReadOnlyList<AdaptMoment> Moments)
{
    public IEnumerable<AdaptMoment> Challenges => Moments.Where(m => m.IsChallenge);
    public int ChallengeCount => Challenges.Count();
    public int AdaptedCount => Challenges.Count(m => m.Adapted);
    public double? Rate => ChallengeCount == 0 ? null : (double)AdaptedCount / ChallengeCount;
}

public sealed record BehaviorProfile(
    IReadOnlyList<AxisReading> Axes,
    IReadOnlyList<AxisReading> Strengths,
    IReadOnlyList<SituationalTendency> Situational,
    AdaptabilitySummary Adaptability,
    IReadOnlyList<string> BlindSpots,
    IReadOnlyList<string> Stretches,
    IReadOnlyList<StrategyEvidence> Strategies);

/// <summary>
/// Behavior lab analysis. Usual tendencies come from situations with no stated demand;
/// context tags reveal how those tendencies shift; demand-tagged situations measure adaptability.
/// </summary>
public sealed class BehaviorAnalyzer(TestCatalog catalog)
{
    public const double ChallengeThreshold = 0.2;
    public const double AdaptedThreshold = 0.3;
    private const double StrengthThreshold = 0.5;
    private const double ShiftThreshold = 0.5;

    public async Task<List<Observation>> ObserveAsync(IEnumerable<TestResult> results)
    {
        var list = new List<Observation>();
        foreach (var r in results)
        {
            var test = ScenarioSequence.DefinitionFor(r, await catalog.GetTestAsync(r.TestId));
            if (test is null || test.Lab == Labs.Philosophy) continue;
            foreach (var q in test.Questions)
            {
                var values = Scoring.Contributions(q, r.For(q.Id));
                if (values.Count > 0) list.Add(new(test, q, values, r.CompletedAt));
            }
        }
        return list;
    }

    /// <summary>Your usual style: every answered situation that didn't call for a particular style.</summary>
    public static Dictionary<string, DimensionTally> Baseline(IEnumerable<Observation> observations)
    {
        var tallies = new Dictionary<string, DimensionTally>();
        foreach (var o in observations.Where(o => o.Question.Demands.Count == 0))
            foreach (var (d, v) in o.Values)
                Scoring.Add(tallies, d, v);
        return tallies;
    }

    public async Task<BehaviorProfile> BuildAsync(IReadOnlyList<TestResult> latestResults)
    {
        var dims = (await catalog.GetDimensionsAsync(Labs.Behavior)).ToDictionary(d => d.Id);
        var contexts = (await catalog.GetContextsAsync()).ToDictionary(c => c.Id);
        var obs = await ObserveAsync(latestResults);
        var baseline = Baseline(obs);

        var axes = dims.Values
            .Where(d => baseline.ContainsKey(d.Id))
            .Select(d => new AxisReading(d, baseline[d.Id].Score, baseline[d.Id].Weight))
            .ToList();

        var strengths = axes
            .Where(a => a.Weight >= 3 && Math.Abs(a.Score) >= StrengthThreshold)
            .OrderByDescending(a => Math.Abs(a.Score))
            .ToList();

        var adapt = Adaptability(obs, baseline, dims);

        var blindSpots = adapt.Challenges.Where(m => !m.Adapted).Select(m =>
            $"In this scenario, your choice stayed near your earlier preference for {m.Dimension.LeanFor(m.Baseline)} when {m.Observation.Question.Need}.")
            .Distinct().ToList();

        var stretches = adapt.Challenges.Where(m => m.Adapted).Select(m =>
            $"In this scenario, you selected the suggested alternative to {m.Dimension.LeanFor(m.Baseline)} when {m.Observation.Question.Need}.")
            .Distinct().ToList();

        return new BehaviorProfile(axes, strengths, Situational(obs, dims, contexts), adapt, blindSpots, stretches,
            await EvidenceAsync(latestResults));
    }

    public static AdaptabilitySummary Adaptability(
        IEnumerable<Observation> observations,
        Dictionary<string, DimensionTally> baseline,
        IReadOnlyDictionary<string, Dimension> dims)
    {
        var moments = new List<AdaptMoment>();
        foreach (var o in observations)
            foreach (var (d, direction) in o.Question.Demands)
            {
                if (!o.Values.TryGetValue(d, out var v) || !dims.TryGetValue(d, out var dim)) continue;
                var b = baseline.TryGetValue(d, out var t) && t.Weight >= 2 ? t.Score : 0;
                var sign = Math.Sign(direction);
                moments.Add(new(o, dim, b,
                    IsChallenge: sign * b <= -ChallengeThreshold,
                    Adapted: sign * v >= AdaptedThreshold));
            }
        return new AdaptabilitySummary(moments);
    }


    public async Task<List<StrategyEvidence>> EvidenceAsync(IEnumerable<TestResult> results)
    {
        var opportunities = new Dictionary<string, int>();
        var chosen = new Dictionary<string, List<ChoiceEvidence>>();
        foreach (var r in results)
        {
            var test = ScenarioSequence.DefinitionFor(r, await catalog.GetTestAsync(r.TestId));
            if (test is null || test.Lab == Labs.Philosophy) continue;
            foreach (var q in test.Questions.Where(q => q.Type == QuestionType.Choice))
            {
                var a = r.For(q.Id);
                if (!Scoring.IsAnswered(q, a)) continue;
                foreach (var tag in q.Options.SelectMany(o => o.Strategies).Distinct())
                    opportunities[tag] = opportunities.GetValueOrDefault(tag) + 1;
                var selected = q.Options.First(o => o.Id == a!.ChoiceId);
                foreach (var tag in selected.Strategies.Distinct())
                {
                    if (!chosen.TryGetValue(tag, out var examples)) chosen[tag] = examples = [];
                    examples.Add(new(r.Id, test.Title, q.Prompt, selected.Label, ScenarioSequence.Context(q, r.Answers)));
                }
            }
        }
        return chosen.OrderByDescending(kv => kv.Value.Count).Select(kv =>
            new StrategyEvidence(kv.Key, kv.Value.Count, opportunities[kv.Key], kv.Value)).ToList();
    }

    private static List<SituationalTendency> Situational(
        List<Observation> obs,
        IReadOnlyDictionary<string, Dimension> dims,
        IReadOnlyDictionary<string, SituationContext> contexts)
    {
        var usual = obs.Where(o => o.Question.Demands.Count == 0).ToList();
        var shifts = new List<(Dimension Dim, int Sign, string When, double Size)>();

        foreach (var ctx in contexts.Values)
            foreach (var dim in dims.Values)
            {
                var inside = usual.Where(o => o.Question.Contexts.Contains(ctx.Id) && o.Values.ContainsKey(dim.Id))
                                  .Select(o => o.Values[dim.Id]).ToList();
                var outside = usual.Where(o => !o.Question.Contexts.Contains(ctx.Id) && o.Values.ContainsKey(dim.Id))
                                   .Select(o => o.Values[dim.Id]).ToList();
                if (inside.Count < 2 || outside.Count < 2) continue;

                var diff = inside.Average() - outside.Average();
                if (Math.Abs(diff) >= ShiftThreshold)
                    shifts.Add((dim, Math.Sign(diff), ctx.When, Math.Abs(diff)));
            }

        return shifts
            .GroupBy(s => (s.Dim.Id, s.Sign))
            .OrderByDescending(g => g.Max(s => s.Size))
            .Take(6)
            .Select(g =>
            {
                var dim = g.First().Dim;
                var shift = $"Your responses leaned more toward {dim.PoleFor(g.Key.Sign).ToLowerInvariant()}";
                return new SituationalTendency(dim, shift ?? $"you lean {dim.PoleFor(g.Key.Sign).ToLowerInvariant()}",
                    g.OrderByDescending(s => s.Size).Select(s => s.When + " (at least 2 responses in each comparison group)").ToList());
            })
            .ToList();
    }
}
