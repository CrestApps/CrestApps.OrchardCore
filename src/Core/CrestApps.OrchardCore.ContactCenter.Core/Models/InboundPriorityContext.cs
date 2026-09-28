using CrestApps.OrchardCore.ContactCenter.Models;

namespace CrestApps.OrchardCore.ContactCenter.Core.Models;

/// <summary>
/// What is known about an inbound call at the moment it is about to be queued, which is the only moment where
/// raising a caller's priority still changes where they land in line.
/// </summary>
public sealed class InboundPriorityContext
{
    /// <summary>
    /// Gets or sets the priority the entry point or queue configured. A contributor may raise this; it may not
    /// lower it, because it is an explicit operator decision about this number.
    /// </summary>
    public InteractionPriority ConfiguredPriority { get; set; } = InteractionPriority.Normal;

    /// <summary>
    /// Gets or sets the caller's address.
    /// </summary>
    public string CustomerAddress { get; set; }

    /// <summary>
    /// Gets or sets the number the caller dialled.
    /// </summary>
    public string ServiceAddress { get; set; }

    /// <summary>
    /// Gets or sets the queue the call is heading for.
    /// </summary>
    public string QueueId { get; set; }

    /// <summary>
    /// Gets or sets the CRM contact the call was matched to, when one was.
    /// </summary>
    public string ContactContentItemId { get; set; }

    /// <summary>
    /// Gets or sets the campaign the call belongs to, when it belongs to one.
    /// </summary>
    public string CampaignId { get; set; }

    /// <summary>
    /// Gets or sets when this caller last reached us, when they have before.
    /// </summary>
    public DateTime? LastInboundUtc { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether this call is a caller returning a callback we placed.
    /// </summary>
    public bool IsReturningCallback { get; set; }
}
