namespace CrestApps.OrchardCore.Telephony.Services;

/// <summary>
/// The moment at which a destination is being evaluated.
/// </summary>
public enum DialDestinationOperation
{
    /// <summary>
    /// A new outbound call is being placed.
    /// </summary>
    Dial,

    /// <summary>
    /// A live call is being transferred to the destination.
    /// </summary>
    Transfer,

    /// <summary>
    /// An administrator is saving the destination in a catalog or settings screen.
    /// </summary>
    Configure,
}
