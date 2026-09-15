namespace CrestApps.OrchardCore.Telephony.Services;

/// <summary>
/// The verdict the dial destination policy reached for an address.
/// </summary>
public enum DialDestinationOutcome
{
    /// <summary>
    /// The platform will place or transfer a call to the address.
    /// </summary>
    Allowed,

    /// <summary>
    /// The address is an emergency service. The platform never dials one, because an emergency call placed from a
    /// hosted number reaches the wrong dispatch center and carries the wrong location.
    /// </summary>
    Emergency,

    /// <summary>
    /// The address is a premium-rate service.
    /// </summary>
    Premium,

    /// <summary>
    /// The address is not a dialable destination at all.
    /// </summary>
    Malformed,

    /// <summary>
    /// The address is refused by a tenant or deployment rule rather than by its own shape.
    /// </summary>
    Blocked,
}
