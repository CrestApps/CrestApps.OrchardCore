namespace CrestApps.OrchardCore.Consent;

/// <summary>
/// The answer to a <see cref="ConsentQuery"/>: whether the person opted out, and which source says so.
/// </summary>
public sealed class ConsentStatus
{
    /// <summary>
    /// The answer when no source says the person opted out.
    /// </summary>
    public static readonly ConsentStatus NotOptedOut = new();

    /// <summary>
    /// Gets a value indicating whether the person opted out of the channel.
    /// </summary>
    public bool HasOptedOut
        => !string.IsNullOrEmpty(Source);

    /// <summary>
    /// Gets the name of the first source that says the person opted out, or <see langword="null"/>.
    /// </summary>
    public string Source { get; init; }

    /// <summary>
    /// Returns the answer that <paramref name="source"/> says the person opted out.
    /// </summary>
    /// <param name="source">The source's name.</param>
    public static ConsentStatus OptedOutBy(string source)
    {
        ArgumentException.ThrowIfNullOrEmpty(source);

        return new ConsentStatus { Source = source };
    }
}
