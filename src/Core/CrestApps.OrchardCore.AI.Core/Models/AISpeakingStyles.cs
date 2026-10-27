namespace CrestApps.OrchardCore.AI.Core.Models;

/// <summary>
/// Speaking styles are prompt templates in the <see cref="Category"/> category. The AI module ships the ones below in
/// its <c>Templates/Prompts</c> folder; more are added under <c>Artificial Intelligence &gt; Templates</c> as System
/// Prompt templates in that category, and travel with recipes and deployment plans like any other template.
/// </summary>
public static class AISpeakingStyles
{
    /// <summary>
    /// The template category speaking styles are listed from.
    /// </summary>
    public const string Category = "Speaking style";

    /// <summary>
    /// Reads back names, numbers and other details before acting on them.
    /// </summary>
    public const string EchoVerification = "speaking-style-echo-verification";

    /// <summary>
    /// Spells letters with the NATO alphabet.
    /// </summary>
    public const string NatoSpelling = "speaking-style-nato-spelling";

    /// <summary>
    /// Writes numbers, dates and money the way they are said.
    /// </summary>
    public const string SpeechNormalization = "speaking-style-speech-normalization";

    /// <summary>
    /// Treats names that sound alike as possible matches.
    /// </summary>
    public const string FuzzyNameMatching = "speaking-style-fuzzy-name-matching";

    /// <summary>
    /// Says it is an AI assistant when asked.
    /// </summary>
    public const string AIDisclosure = "speaking-style-ai-disclosure";

    /// <summary>
    /// Stays on the business it is for.
    /// </summary>
    public const string ScopeLimits = "speaking-style-scope-limits";

    /// <summary>
    /// Speaks naturally with occasional filler words.
    /// </summary>
    public const string FillerWords = "speaking-style-filler-words";

    /// <summary>
    /// Acknowledges feelings before solving the problem.
    /// </summary>
    public const string HighEmpathy = "speaking-style-high-empathy";

    /// <summary>
    /// The styles the AI module ships.
    /// </summary>
    public static readonly IReadOnlyList<string> BuiltIn =
    [
        AIDisclosure,
        ScopeLimits,
        HighEmpathy,
        EchoVerification,
        NatoSpelling,
        SpeechNormalization,
        FuzzyNameMatching,
        FillerWords,
    ];

    /// <summary>
    /// Keeps the template identifiers of a selection once each, trimmed and in the order given.
    /// </summary>
    /// <param name="styles">The selection.</param>
    public static string[] Normalize(IEnumerable<string> styles)
        => (styles ?? [])
            .Where(style => !string.IsNullOrWhiteSpace(style))
            .Select(style => style.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
}
