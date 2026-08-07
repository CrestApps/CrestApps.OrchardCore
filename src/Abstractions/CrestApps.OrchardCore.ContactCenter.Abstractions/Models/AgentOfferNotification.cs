namespace CrestApps.OrchardCore.ContactCenter.Models;

/// <summary>
/// Describes a work item being offered to a specific agent in real time so the agent desktop can render
/// the offer without waiting for the telephony ring event.
/// </summary>
public sealed class AgentOfferNotification
{
    /// <summary>
    /// Gets or sets the identifier of the Orchard user the offer is presented to.
    /// </summary>
    public string UserId { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the agent profile the offer is reserved for.
    /// </summary>
    public string AgentId { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the reservation that backs the offer.
    /// </summary>
    public string ReservationId { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the CRM activity being offered.
    /// </summary>
    public string ActivityItemId { get; set; }

    /// <summary>
    /// Gets or sets whether the assigned activity should open automatically in the agent experience.
    /// </summary>
    public bool AutoOpenActivity { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the queue item being offered.
    /// </summary>
    public string QueueItemId { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the queue the offer originated from.
    /// </summary>
    public string QueueId { get; set; }

    /// <summary>
    /// Gets or sets the UTC time the offer expires when it is not accepted.
    /// </summary>
    public DateTime ExpiresUtc { get; set; }

    /// <summary>
    /// Gets or sets the authoritative server UTC time, used by the client to align the countdown timer.
    /// </summary>
    public DateTime ServerTimeUtc { get; set; }
}
