using PhilosophyTester.Models;

namespace PhilosophyTester.Services;

/// <summary>
/// Turns answers into per-dimension tallies. Each question adds weight 1 to every
/// dimension it can affect, so a neutral answer pulls a score toward the middle.
/// </summary>
public static class Scoring
{
    public static Dictionary<string, DimensionTally> Score(TestDefinition test, IEnumerable<Answer> answers)
    {
        var tallies = new Dictionary<string, DimensionTally>();
        var byId = answers.ToDictionary(a => a.QuestionId);

        foreach (var q in test.Questions)
        {
            if (!byId.TryGetValue(q.Id, out var a)) continue;
            foreach (var (dim, value) in Contributions(q, a))
                Add(tallies, dim, value);
        }
        return tallies;
    }

    public static void Add(Dictionary<string, DimensionTally> tallies, string dim, double value, double weight = 1)
    {
        var t = tallies.TryGetValue(dim, out var existing) ? existing : tallies[dim] = new();
        t.Total += value * weight;
        t.Weight += weight;
    }

    public static bool IsAnswered(Question q, Answer? a) => a is not null && q.Type switch
    {
        QuestionType.Choice => q.Options.Any(o => o.Id == a.ChoiceId),
        QuestionType.Allocation => a.Allocations is not null && a.Allocations.All(kv =>
            q.Options.Any(o => o.Id == kv.Key) && kv.Value >= 0 && kv.Value <= q.Points) &&
            a.Allocations.Values.Sum(v => (long)v) == q.Points,
        QuestionType.Scale => a.ScaleValue is int value && value >= q.Min && value <= q.Max,
        QuestionType.Ladder => a.LadderLevel is int level && level >= -1 && level < q.Options.Count,
        QuestionType.Pick => a.Picks is not null && a.Picks.Count == q.PickCount &&
            a.Picks.Distinct().Count() == a.Picks.Count && a.Picks.All(id => q.Options.Any(o => o.Id == id)),
        QuestionType.Assign => a.Assignments is not null && a.Assignments.Count == q.Rows.Count &&
            q.Rows.All(r => a.Assignments.TryGetValue(r.Id, out var id) && q.Options.Any(o => o.Id == id)),
        _ => false
    };

    /// <summary>Maps a position within [min, max] onto [-1, 1].</summary>
    public static double Centered(double value, double min, double max) =>
        max <= min ? 0 : 2 * (value - min) / (max - min) - 1;

    /// <summary>What one answered question contributes to each dimension, each in [-1, 1].</summary>
    public static Dictionary<string, double> Contributions(Question q, Answer? a)
    {
        var result = new Dictionary<string, double>();
        if (!IsAnswered(q, a)) return result;

        var dims = q.Options.SelectMany(o => o.Effects.Keys)
            .Concat(q.Effects.Keys)
            .Concat(q.Rows.SelectMany(r => r.Effects.Values.SelectMany(e => e.Keys)))
            .Distinct();

        foreach (var d in dims)
            result[d] = Math.Clamp(Contribution(q, a!, d), -1, 1);
        return result;
    }

    private static double Contribution(Question q, Answer a, string d)
    {
        switch (q.Type)
        {
            case QuestionType.Choice:
                return q.Options.FirstOrDefault(o => o.Id == a.ChoiceId)?.Effects.GetValueOrDefault(d) ?? 0;

            case QuestionType.Allocation:
            {
                // deviation: 0 at an equal share, +1 at double the equal share, -1 at zero.
                int n = q.Options.Count;
                var items = q.Options.Where(o => o.Effects.ContainsKey(d)).ToList();
                if (items.Count == 0) return 0;
                return items.Sum(o =>
                {
                    double share = (double)a.Allocations.GetValueOrDefault(o.Id) / q.Points;
                    double deviation = Math.Clamp((share - 1.0 / n) * n, -1, 1);
                    return o.Effects[d] * deviation;
                }) / items.Count;
            }

            case QuestionType.Pick:
                return q.Options.Where(o => a.Picks.Contains(o.Id)).Sum(o => o.Effects.GetValueOrDefault(d))
                       / Math.Max(1, q.PickCount);

            case QuestionType.Scale:
                return q.Effects.GetValueOrDefault(d) * Centered(a.ScaleValue!.Value, q.Min, q.Max);

            case QuestionType.Ladder:
                // -1 (refuse everything) .. +1 (accept the top rung)
                return q.Effects.GetValueOrDefault(d) * Centered(a.LadderLevel!.Value, -1, q.Options.Count - 1);

            case QuestionType.Assign:
            {
                var rows = q.Rows.Where(r => r.Effects.Values.Any(e => e.ContainsKey(d))).ToList();
                if (rows.Count == 0) return 0;
                return rows.Sum(r =>
                    r.Effects.TryGetValue(a.Assignments[r.Id], out var e) ? e.GetValueOrDefault(d) : 0) / rows.Count;
            }

            default:
                return 0;
        }
    }

    public static Dictionary<string, DimensionTally> Combine(IEnumerable<Dictionary<string, DimensionTally>> sets)
    {
        var combined = new Dictionary<string, DimensionTally>();
        foreach (var set in sets)
            foreach (var (dim, t) in set)
            {
                var c = combined.TryGetValue(dim, out var existing) ? existing : combined[dim] = new();
                c.Total += t.Total;
                c.Weight += t.Weight;
            }
        return combined;
    }
}
