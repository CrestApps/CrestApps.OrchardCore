using CrestApps.Core.Data.YesSql.Indexes;
using CrestApps.OrchardCore.Omnichannel.Core.Models;

namespace CrestApps.OrchardCore.Omnichannel.Core.Indexes;

/// <summary>
/// Represents the omnichannel activity index.
/// </summary>
public sealed class OmnichannelActivityIndex : CatalogItemIndex
{
    /// <summary>
    /// Gets or sets the document id.
    /// </summary>
    public long DocumentId { get; set; }

    /// <summary>
    /// Gets or sets the interaction type.
    /// </summary>
    public ActivityInteractionType InteractionType { get; set; }

    /// <summary>
    /// Gets or sets the channel.
    /// </summary>
    public string Channel { get; set; }

    /// <summary>
    /// Gets or sets the channel endpoint id.
    /// </summary>
    public string ChannelEndpointId { get; set; }

    /// <summary>
    /// Gets or sets the preferred destination.
    /// </summary>
    public string PreferredDestination { get; set; }

    /// <summary>
    /// Gets or sets the contact content item id.
    /// </summary>
    public string ContactContentItemId { get; set; }

    /// <summary>
    /// Gets or sets the contact content type.
    /// </summary>
    public string ContactContentType { get; set; }

    /// <summary>
    /// Gets or sets the campaign id.
    /// </summary>
    public string CampaignId { get; set; }

    /// <summary>
    /// Gets or sets the subject content type.
    /// </summary>
    public string SubjectContentType { get; set; }

    /// <summary>
    /// Gets or sets the scheduled utc.
    /// </summary>
    public DateTime ScheduledUtc { get; set; }

    /// <summary>
    /// Gets or sets the attempts.
    /// </summary>
    public int Attempts { get; set; }

    /// <summary>
    /// Gets or sets the assigned to id.
    /// </summary>
    public string AssignedToId { get; set; }

    /// <summary>
    /// Gets or sets the assigned to utc.
    /// </summary>
    public DateTime? AssignedToUtc { get; set; }

    /// <summary>
    /// Gets or sets the created by id.
    /// </summary>
    public string CreatedById { get; set; }

    /// <summary>
    /// Gets or sets the disposition id.
    /// </summary>
    public string DispositionId { get; set; }

    /// <summary>
    /// Gets or sets the created utc.
    /// </summary>
    public DateTime CreatedUtc { get; set; }

    /// <summary>
    /// Gets or sets the completed utc.
    /// </summary>
    public DateTime? CompletedUtc { get; set; }

    /// <summary>
    /// Gets or sets the urgency level.
    /// </summary>
    public ActivityUrgencyLevel UrgencyLevel { get; set; }

    /// <summary>
    /// Gets or sets the status.
    /// </summary>
    public ActivityStatus Status { get; set; }
}
