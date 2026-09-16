using PhilosophyTester.Models;
using static PhilosophyTester.Services.BackupValidator;

namespace PhilosophyTester.Services;

public static class CatalogValidator
{
    public static void Validate(IEnumerable<TestDefinition> tests, IEnumerable<Dimension> dimensions, IEnumerable<SituationContext> contexts)
    {
        var all = tests.ToList();
        var dims = dimensions.Select(d => d.Id).ToHashSet();
        var ctx = contexts.Select(c => c.Id).ToHashSet();
        Unique(all.Select(t => t.Id), "tests");
        Unique(dimensions.Select(d => d.Id), "dimensions");
        Unique(contexts.Select(c => c.Id), "contexts");
        foreach (var t in all)
        {
            ValidateTest(t);
            foreach (var q in t.Questions)
            {
                Require(q.Contexts.All(ctx.Contains), $"{t.Id}/{q.Id}: unknown context.");
                Require(Effects(q).SelectMany(e => e.Keys).All(dims.Contains), $"{t.Id}/{q.Id}: unknown dimension.");
            }
        }
    }

    public static void ValidateTest(TestDefinition t)
    {
        Require(!string.IsNullOrWhiteSpace(t.Id) && t.Version > 0 && !string.IsNullOrWhiteSpace(t.Title), "Invalid test header.");
        Require(t.Lab is Labs.Philosophy or Labs.Behavior or Labs.Development, "Unknown lab.");
        Require(t.Questions is { Count: > 0 }, "Test needs questions.");
        Unique(t.Questions!.Select(q => q?.Id), "questions");
        var earlier = new Dictionary<string, Question>();
        foreach (var q in t.Questions)
        {
            Require(Enum.IsDefined(q.Type) && !string.IsNullOrWhiteSpace(q.Prompt), "Invalid question.");
            Require(q.Options is not null && q.Rows is not null && q.Effects is not null && q.Demands is not null && q.Contexts is not null && q.Variants is not null, "Missing question collections.");
            Unique(q.Options!.Select(o => o?.Id), "options");
            Unique(q.Rows!.Select(r => r?.Id), "rows");
            foreach (var o in q.Options)
                Require(!string.IsNullOrWhiteSpace(o.Label) && o.Effects is not null && o.Strategies is not null && o.Strategies.All(s => !string.IsNullOrWhiteSpace(s)), "Invalid option.");
            foreach (var r in q.Rows)
                Require(!string.IsNullOrWhiteSpace(r.Label) && r.Effects is not null && r.Effects.All(e => q.Options.Any(o => o.Id == e.Key) && e.Value is not null), "Invalid row.");
            Require(q.Type == QuestionType.Scale || q.Options.Count > 0, "Question needs options.");
            Require(q.Type != QuestionType.Scale || q.Min < q.Max, "Invalid scale bounds.");
            Require(q.Type != QuestionType.Pick || q.PickCount > 0 && q.PickCount <= q.Options.Count, "Invalid pick count.");
            Require(q.Type != QuestionType.Allocation || q.Points > 0 && q.Step > 0 && q.Points % q.Step == 0, "Invalid allocation pool.");
            Require(q.Type != QuestionType.Assign || q.Rows.Count > 0, "Assignment needs rows.");
            // Pick weights are summed then divided by PickCount. Preserve the original lifeboat weighting.
            var limit = q.Type == QuestionType.Pick ? q.PickCount : 1;
            Require(Effects(q).SelectMany(e => e.Values).All(v => double.IsFinite(v) && Math.Abs(v) <= limit), "Effect outside allowed range.");
            Require(q.Demands!.Values.All(v => v is -1 or 1) && (q.Demands.Count == 0 || !string.IsNullOrWhiteSpace(q.Need)), "Demand needs a direction and rationale.");
            foreach (var v in q.Variants!)
            {
                Require(v is not null && !string.IsNullOrWhiteSpace(v.Text), "Empty consequence.");
                Require(earlier.TryGetValue(v!.QuestionId, out var source) &&
                    (v.RowId is null ? source.Type == QuestionType.Choice : source.Type == QuestionType.Assign && source.Rows.Any(r => r.Id == v.RowId)) &&
                    source.Options.Any(o => o.Id == v.OptionId), "Consequence must reference an earlier choice or assignment.");
                Require(!t.Dynamic, "Dynamic sets cannot contain dependent steps.");
            }
            earlier.Add(q.Id, q);
        }
    }

    private static IEnumerable<Dictionary<string, double>> Effects(Question q) =>
        new[] { q.Effects, q.Demands }.Concat(q.Options.Select(o => o.Effects)).Concat(q.Rows.SelectMany(r => r.Effects.Values));
}
