namespace CrestApps.OrchardCore.ContactCenter.Core.Models;

/// <summary>
/// Carries the queue context required to continue routing after an agent declines an offer.
/// </summary>
public sealed class OfferDeclinedEventData
{
    /// <summary>
    /// Gets or sets the queue whose next eligible work offer should be evaluated.
    /// </summary>
    public string QueueId { get; set; }
}
