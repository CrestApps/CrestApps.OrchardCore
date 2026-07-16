namespace CrestApps.OrchardCore.ContactCenter.Models;

/// <summary>
/// Describes an offer that is no longer presented to an agent so the agent desktop can dismiss the
/// pending offer and supervisor dashboards can update.
/// </summary>
public sealed class AgentOfferRevokedNotification
{
    /// <summary>
    /// Gets or sets the identifier of the Orchard user the offer was presented to.
    /// </summary>
    public string UserId { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the agent profile the offer was reserved for.
    /// </summary>
    public string AgentId { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the reservation that backed the offer.
    /// </summary>
    public string ReservationId { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the queue the offer originated from.
    /// </summary>
    public string QueueId { get; set; }

    /// <summary>
    /// Gets or sets the reason the offer was revoked.
    /// </summary>
    public AgentOfferRevokedReason Reason { get; set; }
}
