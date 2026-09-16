using PhilosophyTester.Models;

namespace PhilosophyTester.Services;

public static class AnswerFormatter
{
    public static string Describe(Question q, Answer? a)
    {
        if (a is null || !Scoring.IsAnswered(q, a)) return "Skipped";

        string Label(string? id) => q.Options.FirstOrDefault(o => o.Id == id)?.Label ?? id ?? "";

        return q.Type switch
        {
            QuestionType.Choice => Label(a.ChoiceId),
            QuestionType.Pick => string.Join(", ", a.Picks.Select(Label)),
            QuestionType.Allocation => string.Join(", ",
                q.Options.Where(o => a.Allocations.GetValueOrDefault(o.Id) > 0)
                         .OrderByDescending(o => a.Allocations[o.Id])
                         .Select(o => $"{o.Label} {a.Allocations[o.Id]}")),
            QuestionType.Scale => $"{a.ScaleValue} of {q.Max} ({q.MinLabel} → {q.MaxLabel})",
            QuestionType.Ladder => a.LadderLevel < 0
                ? q.NoneLabel ?? "None"
                : q.Options[a.LadderLevel!.Value].Label,
            QuestionType.Assign => string.Join("; ",
                q.Rows.Select(r => $"{r.Label}: {Label(a.Assignments.GetValueOrDefault(r.Id))}")),
            _ => ""
        };
    }
}
