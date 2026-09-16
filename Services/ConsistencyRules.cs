using PhilosophyTester.Models;

namespace PhilosophyTester.Services;

/// <summary>
/// Compares answers across (or within) tests and reports interesting tensions.
/// Rules only fire when every answer they need exists. Register new rules in Program.cs.
/// </summary>
public interface IConsistencyRule
{
    IEnumerable<Tension> Evaluate(IReadOnlyDictionary<string, TestResult> latestByTest);
}

internal static class RuleHelpers
{
    public static Answer? Get(this IReadOnlyDictionary<string, TestResult> r, string testId, string questionId) =>
        r.TryGetValue(testId, out var result) ? result.For(questionId) : null;
}

/// <summary>Stated "family first" vs. sacrificing a spouse for five strangers.</summary>
public sealed class FamilyVersusStrangersRule : IConsistencyRule
{
    public IEnumerable<Tension> Evaluate(IReadOnlyDictionary<string, TestResult> r)
    {
        var circle = r.Get("family-priority", "circle");
        var spouse = r.Get("trolley", "spouse");
        if (circle is null || spouse?.ChoiceId is null) yield break;

        int family = circle.Allocations.GetValueOrDefault("spouse") + circle.Allocations.GetValueOrDefault("children");
        int strangers = circle.Allocations.GetValueOrDefault("strangers");

        if (family > strangers && spouse.ChoiceId == "divert")
            yield return new("Family first — until the numbers grew",
                $"You gave your spouse and children {family} priority points and strangers {strangers}, " +
                "yet on the tracks you sent the train toward your spouse to save five strangers.");
    }
}

/// <summary>Who got the most points vs. who got the free weekend.</summary>
public sealed class StatedVersusWeekendRule : IConsistencyRule
{
    private static readonly Dictionary<string, (string Item, string Name)> WeekendToItem = new()
    {
        ["spouse"] = ("spouse", "your spouse"),
        ["child"] = ("children", "your child"),
        ["parents"] = ("parents", "your parents"),
        ["friend"] = ("friends", "your best friend"),
    };

    public IEnumerable<Tension> Evaluate(IReadOnlyDictionary<string, TestResult> r)
    {
        var circle = r.Get("family-priority", "circle");
        var weekend = r.Get("family-priority", "weekend");
        if (circle is null || weekend?.ChoiceId is null || !WeekendToItem.TryGetValue(weekend.ChoiceId, out var chosen))
            yield break;

        var top = WeekendToItem.Values
            .OrderByDescending(v => circle.Allocations.GetValueOrDefault(v.Item))
            .First();

        if (circle.Allocations.GetValueOrDefault(chosen.Item) < circle.Allocations.GetValueOrDefault(top.Item))
            yield return new("Your chart and your weekend disagree",
                $"On paper {top.Name} ranked highest, but the free weekend went to {chosen.Name}. " +
                "Urgency may be outranking closeness for you.");
    }
}

/// <summary>The classic lever/push split.</summary>
public sealed class LeverVersusPushRule : IConsistencyRule
{
    public IEnumerable<Tension> Evaluate(IReadOnlyDictionary<string, TestResult> r)
    {
        var lever = r.Get("trolley", "classic")?.ChoiceId;
        var push = r.Get("trolley", "push")?.ChoiceId;
        if (lever == "pull" && push == "refuse")
            yield return new("Same numbers, different answer",
                "You'd pull a lever to trade one life for five, but not push someone to do it. " +
                "How a harm happens matters to you, not only how many are harmed.");
    }
}

public sealed class LoyaltyClosenessRule : IConsistencyRule
{
    public IEnumerable<Tension> Evaluate(IReadOnlyDictionary<string, TestResult> r)
    {
        var friend = r.Get("loyalty", "friend-theft")?.ChoiceId;
        var coworker = r.Get("loyalty", "coworker-theft")?.ChoiceId;
        if (friend is not null && friend != "report" && coworker == "report")
            yield return new("Where your loyalty stops",
                "You'd report a coworker you barely know for stealing, but not your best friend for the same act.");
    }
}

public sealed class LibertyVersusMonitoringRule : IConsistencyRule
{
    public IEnumerable<Tension> Evaluate(IReadOnlyDictionary<string, TestResult> r)
    {
        var general = r.Get("privacy", "general")?.ScaleValue;
        var monitor = r.Get("privacy", "monitor-90")?.ChoiceId;
        if (general >= 5 && monitor == "accept")
            yield return new("Freedom has a price for you",
                "You said you'd prefer a free society with more risk, but you'd accept total monitoring for a 90% drop in violent crime.");
    }
}

public sealed class SacrificeCurveRule : IConsistencyRule
{
    public IEnumerable<Tension> Evaluate(IReadOnlyDictionary<string, TestResult> r)
    {
        var friend = r.Get("sacrifice", "friend")?.LadderLevel;
        var stranger = r.Get("sacrifice", "stranger")?.LadderLevel;
        if (friend is not null && stranger is not null && stranger > friend)
            yield return new("Strangers before friends",
                "You'd give up more to save a stranger than to save your best friend. That's rare — it's worth asking yourself why.");
    }
}

// ---------- Behavior lab ----------

/// <summary>How you like to learn vs. how you teach others.</summary>
public sealed class TeachVersusLearnRule : IConsistencyRule
{
    public IEnumerable<Tension> Evaluate(IReadOnlyDictionary<string, TestResult> r)
    {
        if (!r.TryGetValue("learning", out var learn) || !r.TryGetValue("teaching", out var teach)) yield break;
        var l = learn.Tallies.GetValueOrDefault("learning")?.Score ?? 0;
        var t = teach.Tallies.GetValueOrDefault("teaching")?.Score ?? 0;

        if (l >= 0.3 && t <= -0.3)
            yield return new("You teach differently than you learn",
                "You like to learn by diving in, but you teach mostly by explaining. The people you teach may want the same room to explore that you do.",
                Labs.Behavior);
        else if (l <= -0.3 && t >= 0.3)
            yield return new("You teach differently than you learn",
                "You prefer clear instruction when you learn, but you teach mostly through questions and discovery. Some learners may want the directness you value.",
                Labs.Behavior);
    }
}

/// <summary>Challenging others in public vs. being challenged in public.</summary>
public sealed class PublicChallengeRule : IConsistencyRule
{
    public IEnumerable<Tension> Evaluate(IReadOnlyDictionary<string, TestResult> r)
    {
        var challenge = r.Get("teamwork", "bad-leader")?.ChoiceId;
        var received = r.Get("feedback", "boss-public")?.ChoiceId;
        if (challenge == "public" && received is "respond" or "silent")
            yield return new("Public in one direction",
                "You'd challenge a struggling leader in front of the team, but when you're criticized in front of others, you push back or withdraw.",
                Labs.Behavior);
    }
}

public sealed class OwnershipVersusBoundariesRule : IConsistencyRule
{
    public IEnumerable<Tension> Evaluate(IReadOnlyDictionary<string, TestResult> r)
    {
        var notMine = r.Get("work", "not-mine")?.ChoiceId;
        var sixty = r.Get("work", "sixty-hours")?.ChoiceId;
        if (notMine == "handle" && sixty == "take")
            yield return new("Everything becomes yours",
                "You'd take on an emergency that isn't yours, and an extra project while your family is already feeling your hours.",
                Labs.Behavior);
    }
}

// ---------- Beliefs vs. behavior ----------

public sealed class HonestyUnderDeadlineRule : IConsistencyRule
{
    public IEnumerable<Tension> Evaluate(IReadOnlyDictionary<string, TestResult> r)
    {
        var coworker = r.Get("loyalty", "coworker-theft")?.ChoiceId;
        var brother = r.Get("loyalty", "brother-crime")?.ChoiceId;
        var deadline = r.Get("conflict", "dishonest")?.ChoiceId;
        if (deadline == "go-along" && (coworker == "report" || brother == "truth"))
            yield return new("Principled on paper, flexible under a deadline",
                "In the beliefs lab you chose honesty even at a cost to people you know. At work, with a deadline looming, you'd go along with telling a client something untrue.",
                Labs.Cross);
    }
}

public sealed class NeedVersusAccountabilityRule : IConsistencyRule
{
    public IEnumerable<Tension> Evaluate(IReadOnlyDictionary<string, TestResult> r)
    {
        if (!r.TryGetValue("justice", out var justice) || !r.TryGetValue("mentor", out var mentor)) yield break;
        var fairness = justice.Tallies.GetValueOrDefault("fairness")?.Score ?? 0;
        var support = mentor.Tallies.GetValueOrDefault("support")?.Score ?? 0;

        if (fairness <= -0.3 && support <= -0.3)
            yield return new("Need counts — until it's your team",
                "When dividing money you weighed people's circumstances, but with a struggling employee you move quickly to accountability.",
                Labs.Cross);
        else if (fairness >= 0.3 && support >= 0.3)
            yield return new("Merit in theory, patience in practice",
                "When dividing money you rewarded what people earned, but with a struggling employee you keep coaching for a long time.",
                Labs.Cross);
    }
}
