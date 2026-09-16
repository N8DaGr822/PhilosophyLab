namespace PhilosophyTester.Models;

/// <summary>A philosophical axis. Scores run from -1 (LowPole) to +1 (HighPole).</summary>
public sealed class Dimension
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string LowPole { get; set; } = "";
    public string HighPole { get; set; } = "";
    public string LowSummary { get; set; } = "";
    public string HighSummary { get; set; } = "";
    /// <summary>"philosophy" or "behavior".</summary>
    public string Lab { get; set; } = Labs.Philosophy;
    /// <summary>Behavior lab: how a shift toward each pole reads, e.g. "you get more directive".</summary>
    public string? LowShift { get; set; }
    public string? HighShift { get; set; }
    /// <summary>Behavior lab: a short name for each pole as a strength, e.g. "Hands-on learning".</summary>
    public string? LowStrength { get; set; }
    public string? HighStrength { get; set; }

    public string PoleFor(double score) => score >= 0 ? HighPole : LowPole;

    /// <summary>Behavior lab: "learning by doing", falling back to the pole name.</summary>
    public string LeanFor(double score) =>
        ((score >= 0 ? HighStrength : LowStrength) ?? PoleFor(score)).ToLowerInvariant();
}

public static class Labs
{
    public const string Philosophy = "philosophy";
    public const string Behavior = "behavior";
    public const string Development = "development";
    /// <summary>Tension scope for rules that compare beliefs with behavior.</summary>
    public const string Cross = "cross";

    public static string Title(string lab) => lab switch
    {
        Behavior => "Behavior lab",
        Development => "Growth",
        _ => "Beliefs lab",
    };
}

/// <summary>A situation tag, e.g. "pressure". Used to find how tendencies change by context.</summary>
public sealed class SituationContext
{
    public string Id { get; set; } = "";
    /// <summary>Completes "When ___, ...", e.g. "time is short".</summary>
    public string When { get; set; } = "";
}

public sealed class TestManifest
{
    public List<string> Tests { get; set; } = [];
}

public sealed class TestDefinition
{
    public string Id { get; set; } = "";
    public int Version { get; set; } = 1;
    public string Lab { get; set; } = Labs.Philosophy;
    /// <summary>Dynamic tests show a personalized subset of their questions (the growth challenge set).</summary>
    public bool Dynamic { get; set; }
    public string Title { get; set; } = "";
    public string Category { get; set; } = "";
    public string Icon { get; set; } = "";
    public string Summary { get; set; } = "";
    public string Intro { get; set; } = "";
    public int MinutesEstimate { get; set; } = 5;
    public List<Question> Questions { get; set; } = [];
}

public enum QuestionType
{
    /// <summary>Pick one option. Option effects are applied directly.</summary>
    Choice,
    /// <summary>Distribute a fixed pool of points. Effects scale with how far each item is above/below an equal share.</summary>
    Allocation,
    /// <summary>A slider between two labelled ends. Question effects scale from -1 (min) to +1 (max).</summary>
    Scale,
    /// <summary>Ordered rungs of rising cost; the user picks the highest rung they'd accept. Question effects scale with height.</summary>
    Ladder,
    /// <summary>Choose exactly PickCount options. Effects of picked options are averaged over PickCount.</summary>
    Pick,
    /// <summary>Give each row (a task) to one of the options (people). Row effects are keyed by option id.</summary>
    Assign
}

public sealed class Question
{
    public List<ScenarioVariant> Variants { get; set; } = [];
    public string Id { get; set; } = "";
    public QuestionType Type { get; set; }
    public string Prompt { get; set; } = "";
    public string? Scenario { get; set; }
    public List<Option> Options { get; set; } = [];

    // Allocation
    public int Points { get; set; } = 100;
    public int Step { get; set; } = 5;

    // Pick
    public int PickCount { get; set; } = 1;

    // Scale
    public int Min { get; set; } = 1;
    public int Max { get; set; } = 7;
    public string? MinLabel { get; set; }
    public string? MaxLabel { get; set; }

    // Ladder: label for "none of these"
    public string? NoneLabel { get; set; }

    /// <summary>Used by Scale and Ladder questions.</summary>
    public Dictionary<string, double> Effects { get; set; } = [];

    /// <summary>Optional prompt shown after answering, asking the user to explain themselves.</summary>
    public bool AskWhy { get; set; }

    // Assign
    public List<AssignRow> Rows { get; set; } = [];

    /// <summary>Behavior lab: situation tags such as "pressure" or "danger".</summary>
    public List<string> Contexts { get; set; } = [];

    /// <summary>
    /// What this situation calls for, as dimension → direction (-1 or +1).
    /// Used to measure whether people adapt when the situation runs against their usual style.
    /// </summary>
    public Dictionary<string, double> Demands { get; set; } = [];

    /// <summary>Completes "when ___", e.g. "a student needed step-by-step instruction".</summary>
    public string? Need { get; set; }
}

public sealed class AssignRow
{
    public string Id { get; set; } = "";
    public string Label { get; set; } = "";
    public string? Detail { get; set; }
    /// <summary>option id → effects when that option is chosen for this row.</summary>
    public Dictionary<string, Dictionary<string, double>> Effects { get; set; } = [];
}

public sealed class Option
{
    public List<string> Strategies { get; set; } = [];
    public string Id { get; set; } = "";
    public string Label { get; set; } = "";
    public string? Detail { get; set; }
    public Dictionary<string, double> Effects { get; set; } = [];
}

/// <summary>A stated fictional consequence of an earlier choice or assignment.</summary>
public sealed class ScenarioVariant
{
    public string QuestionId { get; set; } = "";
    public string? RowId { get; set; }
    public string OptionId { get; set; } = "";
    public string Text { get; set; } = "";
}
