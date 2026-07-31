namespace CrestApps.OrchardCore.Omnichannel.Core.Models;

/// <summary>
/// Specifies the primary communication direction for a subject flow.
/// </summary>
public enum SubjectDirection
{
    /// <summary>
    /// The subject is contacted proactively (for example, an outbound SMS or dial).
    /// </summary>
    Outbound,

    /// <summary>
    /// The subject is engaged in response to an incoming contact from the customer.
    /// </summary>
    Inbound,
}
