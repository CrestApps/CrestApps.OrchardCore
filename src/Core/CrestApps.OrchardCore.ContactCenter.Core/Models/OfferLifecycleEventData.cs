namespace CrestApps.OrchardCore.ContactCenter.Core.Models;

/// <summary>
/// An offer of work to an agent, from the moment it rings to the moment it is settled: the payload of
/// <see cref="ContactCenterConstants.Events.OfferPresented"/>, <see cref="ContactCenterConstants.Events.OfferAccepted"/>,
/// <see cref="ContactCenterConstants.Events.OfferDeclined"/>, <see cref="ContactCenterConstants.Events.OfferExpired"/>,
/// <see cref="ContactCenterConstants.Events.OfferMissed"/> and <see cref="ContactCenterConstants.Events.OfferCancelled"/>.
/// </summary>
/// <remarks>
/// Every offer event names its interaction, so ring time and missed offers can be reported per call and per agent.
/// </remarks>
public sealed class OfferLifecycleEventData
{
    /// <summary>
    /// Gets or sets the reservation identifier.
    /// </summary>
    public string ReservationId { get; set; }

    /// <summary>
    /// Gets or sets the interaction being offered.
    /// </summary>
    public string InteractionId { get; set; }

    /// <summary>
    /// Gets or sets the CRM activity being offered.
    /// </summary>
    public string ActivityItemId { get; set; }

    /// <summary>
    /// Gets or sets the queue the offer came from.
    /// </summary>
    public string QueueId { get; set; }

    /// <summary>
    /// Gets or sets the campaign the offer came from, for outbound work.
    /// </summary>
    public string CampaignId { get; set; }

    /// <summary>
    /// Gets or sets the agent the offer was made to.
    /// </summary>
    public string AgentId { get; set; }

    /// <summary>
    /// Gets or sets the user the agent signs in as.
    /// </summary>
    public string UserId { get; set; }

    /// <summary>
    /// Gets or sets the channel of the work, such as Phone or Sms.
    /// </summary>
    public string Channel { get; set; }

    /// <summary>
    /// Gets or sets why the offer ended the way it did, when it was not accepted.
    /// </summary>
    public string Reason { get; set; }

    /// <summary>
    /// Gets or sets when the offer started ringing.
    /// </summary>
    public DateTime? PresentedUtc { get; set; }

    /// <summary>
    /// Gets or sets when the offer was settled, for any event after it was presented.
    /// </summary>
    public DateTime? SettledUtc { get; set; }

    /// <summary>
    /// Gets or sets how long the offer rang, in seconds, once settled.
    /// </summary>
    public double? RingSeconds { get; set; }
}
