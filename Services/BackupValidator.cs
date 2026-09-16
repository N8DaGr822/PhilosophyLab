using System.Text.Json;
using PhilosophyTester.Models;

namespace PhilosophyTester.Services;

public static class BackupValidator
{
    public static DeviceData Parse(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            Require(root.ValueKind == JsonValueKind.Object && root.TryGetProperty("schemaVersion", out _) &&
                root.TryGetProperty("people", out _) && root.TryGetProperty("results", out _), "Missing backup fields.");
            Require(root.GetProperty("people").ValueKind == JsonValueKind.Array && root.GetProperty("results").ValueKind == JsonValueKind.Array,
                "People and results must be lists.");
            foreach (var person in root.GetProperty("people").EnumerateArray())
                Fields(person, "id", "name", "pinnedTests");
            foreach (var result in root.GetProperty("results").EnumerateArray())
            {
                Fields(result, "id", "profileId", "testId", "testVersion", "completedAt", "answers", "tallies");
                Require(result.GetProperty("answers").ValueKind == JsonValueKind.Array, "Answers must be a list.");
                foreach (var answer in result.GetProperty("answers").EnumerateArray()) Fields(answer, "questionId");
            }
            var data = JsonSerializer.Deserialize(json, AppJsonContext.Default.DeviceData);
            Require(data is not null, "Empty backup.");
            Validate(data!);
            return data!;
        }
        catch (JsonException ex) { throw new FormatException("Invalid backup JSON.", ex); }
    }

    public static void Validate(DeviceData data)
    {
        Require(data.SchemaVersion == 1, "Unsupported backup version.");
        Require(data.People is not null && data.Results is not null, "People and results must be lists.");
        Unique(data.People!.Select(p => p?.Id), "people");
        Unique(data.Results!.Select(r => r?.Id), "results");
        foreach (var p in data.People)
        {
            Require(!string.IsNullOrWhiteSpace(p.Name) && p.Name.Length <= 40, "Invalid person's name.");
            Require(p.PinnedTests is not null, "Missing saved-test list.");
            Unique(p.PinnedTests!, "saved tests");
        }
        foreach (var r in data.Results)
        {
            Require(data.People.Any(p => p.Id == r.ProfileId), "A result refers to a missing person.");
            Require(!string.IsNullOrWhiteSpace(r.TestId) && r.TestVersion > 0, "Invalid test reference.");
            Require(r.Answers is not null && r.Tallies is not null, "Missing answers or scores.");
            Unique(r.Answers!.Select(a => a?.QuestionId), "answers");
            ValidateTallies(r.Tallies!);
            if (r.Baseline is not null) ValidateTallies(r.Baseline);
            foreach (var a in r.Answers)
            {
                Require(a.Allocations is not null && a.Picks is not null && a.Assignments is not null, "Missing answer values.");
                Require(a.Allocations!.All(x => !string.IsNullOrWhiteSpace(x.Key) && x.Value >= 0), "Invalid allocation.");
                Require(a.Assignments!.All(x => !string.IsNullOrWhiteSpace(x.Key) && !string.IsNullOrWhiteSpace(x.Value)), "Invalid assignment.");
                Unique(a.Picks!, "picks");
            }
            if (r.Definition is not null)
            {
                CatalogValidator.ValidateTest(r.Definition);
                Require(r.Definition.Id == r.TestId && r.Definition.Version == r.TestVersion, "Snapshot does not match result.");
                ValidateAnswers(r, r.Definition);
            }
        }
        // Old backups can contain a deleted active ID. Repair only this pointer.
        if (!data.People.Any(p => p.Id == data.ActivePersonId)) data.ActivePersonId = data.People.FirstOrDefault()?.Id;
    }

    public static void ValidateAnswers(TestResult result, TestDefinition definition)
    {
        Require(result.Answers.All(a => definition.Questions.Any(q => q.Id == a.QuestionId && Scoring.IsAnswered(q, a))),
            "An answer is outside its question's allowed values.");
        Require(result.Answers.Count > 0 && ((definition.Dynamic && result.Definition is null) || definition.Questions.Count == result.Answers.Count), "Incomplete result.");
        var expected = Scoring.Score(definition, result.Answers);
        Require(expected.Count == result.Tallies.Count && expected.All(kv => result.Tallies.TryGetValue(kv.Key, out var t) &&
            Math.Abs(t.Total - kv.Value.Total) < 0.000001 && Math.Abs(t.Weight - kv.Value.Weight) < 0.000001), "Scores do not match answers.");
    }

    private static void ValidateTallies(Dictionary<string, DimensionTally> values) => Require(values.All(kv =>
        !string.IsNullOrWhiteSpace(kv.Key) && kv.Value is { } t && double.IsFinite(t.Total) && double.IsFinite(t.Weight) &&
        t.Weight >= 0 && Math.Abs(t.Total) <= t.Weight + 0.000001), "Invalid score values.");

    private static void Fields(JsonElement element, params string[] names) => Require(
        element.ValueKind == JsonValueKind.Object && names.All(name => element.TryGetProperty(name, out _)), "Missing required backup fields.");

    internal static void Unique(IEnumerable<string?> ids, string label)
    {
        var list = ids.ToList();
        Require(list.All(id => !string.IsNullOrWhiteSpace(id)) && list.Distinct().Count() == list.Count, $"Missing or duplicate IDs in {label}.");
    }

    internal static void Require([System.Diagnostics.CodeAnalysis.DoesNotReturnIf(false)] bool condition, string message)
    {
        if (!condition) throw new FormatException(message);
    }
}
