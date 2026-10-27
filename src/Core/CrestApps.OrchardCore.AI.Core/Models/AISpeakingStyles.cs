namespace CrestApps.OrchardCore.AI.Core.Models;

/// <summary>
/// The speaking styles a profile can turn on. Each is a prompt template in the AI module's <c>Templates/Prompts</c>
/// folder named <c>speaking-style-</c> and the style in kebab case, so the wording can be read and changed without
/// editing code.
/// </summary>
public static class AISpeakingStyles
{
    /// <summary>
    /// Reads back names, numbers and other details before acting on them.
    /// </summary>
    public const string EchoVerification = "echo-verification";

    /// <summary>
    /// Spells letters with the NATO alphabet.
    /// </summary>
    public const string NatoSpelling = "nato-spelling";

    /// <summary>
    /// Writes numbers, dates and money the way they are said.
    /// </summary>
    public const string SpeechNormalization = "speech-normalization";

    /// <summary>
    /// Treats names that sound alike as possible matches.
    /// </summary>
    public const string FuzzyNameMatching = "fuzzy-name-matching";

    /// <summary>
    /// Says it is an AI assistant when asked.
    /// </summary>
    public const string AIDisclosure = "ai-disclosure";

    /// <summary>
    /// Stays on the business it is for.
    /// </summary>
    public const string ScopeLimits = "scope-limits";

    /// <summary>
    /// Speaks naturally with occasional filler words.
    /// </summary>
    public const string FillerWords = "filler-words";

    /// <summary>
    /// Acknowledges feelings before solving the problem.
    /// </summary>
    public const string HighEmpathy = "high-empathy";

    /// <summary>
    /// Every style, in the order their sections are added to the system message.
    /// </summary>
    public static readonly IReadOnlyList<string> All =
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
    /// Gets the prompt template that holds a style's section.
    /// </summary>
    /// <param name="style">The style.</param>
    public static string GetTemplateId(string style)
        => "speaking-style-" + style;

    /// <summary>
    /// Keeps the known styles of a selection, once each and in the order of <see cref="All"/>.
    /// </summary>
    /// <param name="styles">The selection.</param>
    public static string[] Normalize(IEnumerable<string> styles)
    {
        var selected = new HashSet<string>(styles ?? [], StringComparer.OrdinalIgnoreCase);

        return All.Where(selected.Contains).ToArray();
    }
}
