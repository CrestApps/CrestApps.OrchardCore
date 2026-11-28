namespace CrestApps.OrchardCore.Surveys;

/// <summary>
/// The built-in kinds of survey. A kind is an open string: modules may use their own.
/// </summary>
public static class SurveyKinds
{
    /// <summary>
    /// A survey or form.
    /// </summary>
    public const string Survey = "Survey";

    /// <summary>
    /// A scored quiz or exam.
    /// </summary>
    public const string Quiz = "Quiz";

    /// <summary>
    /// A one-question poll whose totals respondents may see.
    /// </summary>
    public const string Poll = "Poll";

    /// <summary>
    /// The longest kind a survey stores, the length of its index column.
    /// </summary>
    public const int MaxLength = 50;

    /// <summary>
    /// Gets whether a kind is one a survey may store: a name of ASCII letters, digits, hyphens and underscores that starts
    /// with a letter and fits <see cref="MaxLength"/>. Modules' own kinds follow the same rule, so a kind is never markup.
    /// </summary>
    /// <param name="kind">The kind.</param>
    /// <returns><see langword="true"/> when the kind may be stored.</returns>
    public static bool IsValid(string kind)
    {
        if (string.IsNullOrEmpty(kind) || kind.Length > MaxLength || !char.IsAsciiLetter(kind[0]))
        {
            return false;
        }

        foreach (var character in kind)
        {
            if (!char.IsAsciiLetterOrDigit(character) && character is not '-' and not '_')
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Gets whether a kind is one of the built-in kinds: <see cref="Survey"/>, <see cref="Quiz"/> or <see cref="Poll"/>.
    /// </summary>
    /// <param name="kind">The kind.</param>
    /// <returns><see langword="true"/> for a built-in kind.</returns>
    public static bool IsBuiltIn(string kind)
        => kind is Survey or Quiz or Poll;
}
