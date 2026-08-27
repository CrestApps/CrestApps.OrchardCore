using CrestApps.OrchardCore.ContactCenter.Core.Models;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Builds the virtual, in-memory queue that carries a dialer campaign's outbound work. The queue is never
/// persisted: outbound routing synthesizes it on demand from the campaign queue identifier, so users never see
/// or configure a queue for outbound dialing and no queue record can leak into the queue administration UI.
/// Its configuration is intentionally minimal and default: the dialer profile owns outbound calling-window
/// enforcement through the eligibility gate, so the virtual queue applies no business-hours restriction of its
/// own.
/// </summary>
public static class CampaignRoutingQueue
{
    /// <summary>
    /// Creates the transient virtual campaign queue for the supplied campaign queue identifier.
    /// </summary>
    /// <param name="queueId">The campaign queue identifier.</param>
    /// <returns>A transient <see cref="ActivityQueue"/> that is never stored.</returns>
    public static ActivityQueue Create(string queueId)
    {
        return new ActivityQueue
        {
            ItemId = queueId,
            Name = queueId,
            Enabled = true,
        };
    }
}
