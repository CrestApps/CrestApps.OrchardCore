using CrestApps.OrchardCore.Telephony.Models;

namespace CrestApps.OrchardCore.ContactCenter.Models;

/// <summary>
/// Represents the current ringing inbound offer that should be restored into the soft phone after a
/// refresh or reconnect.
/// </summary>
public sealed class PendingIncomingCallOffer
{
    /// <summary>
    /// Gets or sets the telephony call shown in the soft-phone incoming modal.
    /// </summary>
    public TelephonyCall Call { get; set; }

    /// <summary>
    /// Gets or sets the contextual cards and lifecycle URLs shown alongside the offer.
    /// </summary>
    public IncomingCallContext Context { get; set; }

    /// <summary>
    /// Gets or sets the UTC time the offer expires when it is not accepted.
    /// </summary>
    public DateTime? ExpiresUtc { get; set; }

    /// <summary>
    /// Gets or sets the authoritative server UTC time used by the client to align expiry.
    /// </summary>
    public DateTime ServerTimeUtc { get; set; }
}
