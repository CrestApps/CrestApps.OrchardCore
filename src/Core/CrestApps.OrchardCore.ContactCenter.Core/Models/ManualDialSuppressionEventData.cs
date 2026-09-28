using CrestApps.OrchardCore.ContactCenter.Models;

namespace CrestApps.OrchardCore.ContactCenter.Core.Models;

/// <summary>
/// Represents the auditable payload recorded when the outbound compliance gate suppresses a manual,
/// agent-initiated soft-phone call. Unlike <see cref="DialerSuppressionEventData"/> there is no dialer
/// profile or CRM activity, because a manual call is not part of a campaign.
/// </summary>
public sealed class ManualDialSuppressionEventData
{
    /// <summary>
    /// Gets or sets the reason the call was suppressed.
    /// </summary>
    public DialerSuppressionReason Reason { get; set; }

    /// <summary>
    /// Gets or sets a human-readable explanation of the suppression decision.
    /// </summary>
    public string Description { get; set; }

    /// <summary>
    /// Gets or sets the destination that would have been dialed.
    /// </summary>
    public string Destination { get; set; }
}
