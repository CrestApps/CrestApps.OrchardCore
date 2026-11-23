namespace CrestApps.OrchardCore.Consent.Unsubscribe;

/// <summary>
/// What a valid unsubscribe link says: the address it was sent to and what it stops.
/// </summary>
public sealed class UnsubscribeTicket
{
    /// <summary>
    /// Gets the normalized e-mail address the link was sent to.
    /// </summary>
    public string Address { get; init; }

    /// <summary>
    /// Gets what the link stops.
    /// </summary>
    public UnsubscribeScope Scope { get; init; }
}
