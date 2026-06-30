using CrestApps.OrchardCore.ContactCenter.Models;

namespace CrestApps.OrchardCore.ContactCenter.Core.Models;

/// <summary>
/// Represents the auditable payload recorded when the outbound compliance gate suppresses a dialing attempt.
/// </summary>
public sealed class DialerSuppressionEventData
{
    /// <summary>
    /// Gets or sets the identifier of the dialer profile that governed the attempt.
    /// </summary>
    public string ProfileItemId { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the CRM activity that was suppressed.
    /// </summary>
    public string ActivityItemId { get; set; }

    /// <summary>
    /// Gets or sets the reason the attempt was suppressed.
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
