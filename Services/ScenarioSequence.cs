using PhilosophyTester.Models;

namespace PhilosophyTester.Services;

public static class ScenarioSequence
{
    public static string? Context(Question question, IEnumerable<Answer> answers)
    {
        var byId = answers.ToDictionary(a => a.QuestionId);
        var variant = question.Variants.FirstOrDefault(v =>
            byId.TryGetValue(v.QuestionId, out var a) &&
            (v.RowId is null ? a.ChoiceId : a.Assignments.GetValueOrDefault(v.RowId)) == v.OptionId);
        return variant?.Text ?? question.Scenario;
    }

    public static TestDefinition? DefinitionFor(TestResult result, TestDefinition? current) =>
        result.Definition ?? (current?.Version == result.TestVersion ? current : null);
}
