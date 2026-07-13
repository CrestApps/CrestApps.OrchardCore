using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Models;

namespace CrestApps.OrchardCore.ContactCenter.Core.Models.Reports;

/// <summary>
/// Defines optional dimensions used to filter Contact Center and CRM report populations.
/// </summary>
public sealed class ContactCenterReportCriteria
{
    /// <summary>
    /// Gets or sets the queue identifier used to filter interactions.
    /// </summary>
    public string QueueId { get; set; }

    /// <summary>
    /// Gets or sets the agent profile identifier used to filter interactions.
    /// </summary>
    public string AgentId { get; set; }

    /// <summary>
    /// Gets or sets the campaign identifier used to filter CRM activities.
    /// </summary>
    public string CampaignId { get; set; }

    /// <summary>
    /// Gets or sets the activity source used to filter CRM activities.
    /// </summary>
    public string ActivitySource { get; set; }

    /// <summary>
    /// Gets or sets the channel used to filter interactions and CRM activities.
    /// </summary>
    public InteractionChannel? Channel { get; set; }

    /// <summary>
    /// Gets or sets the interaction direction used to filter interactions.
    /// </summary>
    public InteractionDirection? Direction { get; set; }

    /// <summary>
    /// Gets or sets the activity status used to filter CRM activities.
    /// </summary>
    public ActivityStatus? ActivityStatus { get; set; }
}
