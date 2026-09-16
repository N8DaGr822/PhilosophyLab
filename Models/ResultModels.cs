namespace PhilosophyTester.Models;

public sealed class Answer
{
    public string QuestionId { get; set; } = "";
    public string? ChoiceId { get; set; }
    public Dictionary<string, int> Allocations { get; set; } = [];
    public int? ScaleValue { get; set; }
    /// <summary>Ladder: index of highest accepted rung, -1 for none.</summary>
    public int? LadderLevel { get; set; }
    public List<string> Picks { get; set; } = [];
    /// <summary>Assign: row id → option id.</summary>
    public Dictionary<string, string> Assignments { get; set; } = [];
    public string? Reason { get; set; }
}

public sealed class DimensionTally
{
    public double Total { get; set; }
    public double Weight { get; set; }
    public double Score => Weight == 0 ? 0 : Math.Clamp(Total / Weight, -1, 1);
}

public sealed class TestResult
{
    public TestDefinition? Definition { get; set; }
    public Dictionary<string, DimensionTally>? Baseline { get; set; }
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string ProfileId { get; set; } = "";
    public string TestId { get; set; } = "";
    public int TestVersion { get; set; }
    public DateTimeOffset CompletedAt { get; set; } = DateTimeOffset.Now;
    public List<Answer> Answers { get; set; } = [];
    public Dictionary<string, DimensionTally> Tallies { get; set; } = [];

    public Answer? For(string questionId) => Answers.FirstOrDefault(a => a.QuestionId == questionId);
}

/// <summary>A person on this device. Lets family members keep separate results.</summary>
public sealed class Person
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = "";
    public List<string> PinnedTests { get; set; } = [];
}

/// <summary>Everything saved on the device, stored as one localStorage value.</summary>
public sealed class DeviceData
{
    public int SchemaVersion { get; set; } = 1;
    public string? ActivePersonId { get; set; }
    public List<Person> People { get; set; } = [];
    public List<TestResult> Results { get; set; } = [];
}

/// <summary>Lab is "philosophy", "behavior", or "cross" (shown in both).</summary>
public sealed record Tension(string Title, string Detail, string Lab = Labs.Philosophy);

public static class PersonText
{
    private static bool IsDefault(Person p) => p.Name.Equals("Me", StringComparison.OrdinalIgnoreCase);

    /// <summary>"My list" / "Ava's list"</summary>
    public static string Possessive(this Person p) =>
        IsDefault(p) ? "My" : p.Name.EndsWith('s') ? $"{p.Name}'" : $"{p.Name}'s";

    /// <summary>"How you reason" / "How Ava reasons"</summary>
    public static string HowReasons(this Person p) =>
        IsDefault(p) ? "How you reason" : $"How {p.Name} reasons";

    /// <summary>"How you work" / "How Ava works"</summary>
    public static string HowWorks(this Person p) =>
        IsDefault(p) ? "Your scenario choices" : $"{p.Name}'s scenario choices";
}
