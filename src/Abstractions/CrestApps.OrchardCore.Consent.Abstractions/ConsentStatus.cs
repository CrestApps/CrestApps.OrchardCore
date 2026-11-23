namespace CrestApps.OrchardCore.Consent;

/// <summary>
/// The answer to a <see cref="ConsentQuery"/>: whether the person opted out and which source says so, and which sources
/// couldn't tell.
/// </summary>
/// <remarks>
/// A source that can't tell (see <see cref="ConsentAnswer.Unknown"/>) never makes <see cref="HasOptedOut"/> true:
/// callers that only read <see cref="HasOptedOut"/> treat it as having no opinion, and the other sources still decide.
/// A caller that must not send while a source can't tell checks <see cref="IsUnknown"/> and holds the send.
/// </remarks>
public sealed class ConsentStatus
{
    /// <summary>
    /// The answer when no source says the person opted out and every source could tell.
    /// </summary>
    public static readonly ConsentStatus NotOptedOut = new();

    /// <summary>
    /// Gets a value indicating whether the person opted out of the channel.
    /// </summary>
    public bool HasOptedOut
        => !string.IsNullOrEmpty(Source);

    /// <summary>
    /// Gets a value indicating whether no source says the person opted out but at least one couldn't tell, so the
    /// answer isn't known for sure.
    /// </summary>
    public bool IsUnknown
        => !HasOptedOut && UnknownSources.Count > 0;

    /// <summary>
    /// Gets the name of the first source that says the person opted out, or <see langword="null"/>.
    /// </summary>
    public string Source { get; init; }

    /// <summary>
    /// Gets the names of the sources that couldn't tell, when no source says the person opted out; empty otherwise, and
    /// when every source could tell.
    /// </summary>
    public IReadOnlyList<string> UnknownSources { get; init; } = [];

    /// <summary>
    /// Returns the answer that <paramref name="source"/> says the person opted out.
    /// </summary>
    /// <param name="source">The source's name.</param>
    public static ConsentStatus OptedOutBy(string source)
    {
        ArgumentException.ThrowIfNullOrEmpty(source);

        return new ConsentStatus { Source = source };
    }

    /// <summary>
    /// Returns the answer that no source says the person opted out, but <paramref name="sources"/> couldn't tell.
    /// </summary>
    /// <param name="sources">The names of the sources that couldn't tell.</param>
    public static ConsentStatus UnknownTo(IEnumerable<string> sources)
    {
        ArgumentNullException.ThrowIfNull(sources);

        var names = sources.Where(name => !string.IsNullOrEmpty(name)).Distinct(StringComparer.Ordinal).ToArray();

        return names.Length == 0 ? NotOptedOut : new ConsentStatus { UnknownSources = names };
    }
}
