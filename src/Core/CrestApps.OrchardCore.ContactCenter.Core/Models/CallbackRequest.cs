using CrestApps.Core;
using CrestApps.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Models;

namespace CrestApps.OrchardCore.ContactCenter.Core.Models;

/// <summary>
/// Represents a scheduled callback: a request to dial a customer back at a chosen time. When due, it is
/// promoted into an outbound CRM activity for the dialer or an agent to handle.
/// </summary>
public sealed class CallbackRequest : CatalogItem, IModifiedUtcAwareModel
{
    /// <summary>
    /// Gets or sets the destination number or address to call back.
    /// </summary>
    public string Destination { get; set; }

    /// <summary>
    /// Gets or sets the content item identifier of the contact being called back.
    /// </summary>
    public string ContactContentItemId { get; set; }

    /// <summary>
    /// Gets or sets the content type of the contact being called back.
    /// </summary>
    public string ContactContentType { get; set; }

    /// <summary>
    /// Gets or sets the campaign the callback belongs to.
    /// </summary>
    public string CampaignId { get; set; }

    /// <summary>
    /// Gets or sets the queue the promoted activity is enqueued into, when set.
    /// </summary>
    public string QueueId { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the agent the callback is reserved for, when it is a personal callback.
    /// </summary>
    public string AgentId { get; set; }

    /// <summary>
    /// Gets or sets the lifecycle status of the callback.
    /// </summary>
    public CallbackRequestStatus Status { get; set; }

    /// <summary>
    /// Gets or sets the UTC time the callback was requested.
    /// </summary>
    public DateTime RequestedUtc { get; set; }

    /// <summary>
    /// Gets or sets the UTC time the callback becomes due.
    /// </summary>
    public DateTime ScheduledUtc { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the activity created when the callback was promoted.
    /// </summary>
    public string ActivityItemId { get; set; }

    /// <summary>
    /// Gets or sets the number of dialing attempts made for the callback.
    /// </summary>
    public int Attempts { get; set; }

    /// <summary>
    /// Gets or sets free-form notes recorded with the callback.
    /// </summary>
    public string Notes { get; set; }

    /// <summary>
    /// Gets or sets the UTC time the callback was created.
    /// </summary>
    public DateTime CreatedUtc { get; set; }

    /// <summary>
    /// Gets or sets the UTC time the callback was last modified.
    /// </summary>
    public DateTime? ModifiedUtc { get; set; }
}
