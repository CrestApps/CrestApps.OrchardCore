namespace CrestApps.OrchardCore.ContactCenter.Core.Models;

/// <summary>
/// Identifies the shape of the media topology that joins a call session's legs.
/// </summary>
public enum BridgeKind
{
    /// <summary>
    /// The topology shape has not been determined.
    /// </summary>
    Unknown,

    /// <summary>
    /// Exactly two parties hear one another.
    /// </summary>
    TwoParty,

    /// <summary>
    /// Three or more parties hear one another.
    /// </summary>
    Conference,
}
