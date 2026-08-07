namespace CrestApps.OrchardCore.ContactCenter.ViewModels;

/// <summary>
/// Represents a work item currently offered to the agent, rendered as the ringing offer card on the
/// agent desktop with its accept and decline actions and countdown.
/// </summary>
public sealed class WorkspaceOfferViewModel
{
    /// <summary>
    /// Gets or sets the identifier of the reservation that backs the offer.
    /// </summary>
    public string ReservationId { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the CRM activity being offered.
    /// </summary>
    public string ActivityItemId { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the queue the offer originated from.
    /// </summary>
    public string QueueId { get; set; }

    /// <summary>
    /// Gets or sets the display name of the queue the offer originated from.
    /// </summary>
    public string QueueName { get; set; }

    /// <summary>
    /// Gets or sets the customer label shown on the offer card.
    /// </summary>
    public string CustomerLabel { get; set; }

    /// <summary>
    /// Gets or sets the customer address (for example the caller's phone number) shown on the offer card.
    /// </summary>
    public string CustomerAddress { get; set; }

    /// <summary>
    /// Gets or sets the UTC time the offer expires when it is not accepted.
    /// </summary>
    public DateTime? ExpiresUtc { get; set; }

    /// <summary>
    /// Gets or sets the authoritative server UTC time, used by the client to align the countdown timer.
    /// </summary>
    public DateTime ServerTimeUtc { get; set; }
}
