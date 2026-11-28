namespace CrestApps.OrchardCore.Consent;

/// <summary>
/// What one <see cref="IConsentSource"/> says about a <see cref="ConsentQuery"/>.
/// </summary>
public enum ConsentAnswer
{
    /// <summary>
    /// The source has no opt-out for the person, or doesn't keep that channel or record kind.
    /// </summary>
    NotOptedOut,

    /// <summary>
    /// The source says the person opted out.
    /// </summary>
    OptedOut,

    /// <summary>
    /// The source can't tell right now, for example because its store can't be read. The consent service still asks
    /// the other sources and names this one in <see cref="ConsentStatus.UnknownSources"/>, so each caller decides what
    /// an unknown answer means for its send.
    /// </summary>
    Unknown,
}
