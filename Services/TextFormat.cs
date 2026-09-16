namespace PhilosophyTester.Services;

public static class TextFormat
{
    /// <summary>Capitalizes the first letter: "you plan more" → "You plan more".</summary>
    public static string Sentence(string text) =>
        string.IsNullOrEmpty(text) ? text : char.ToUpperInvariant(text[0]) + text[1..];
}
