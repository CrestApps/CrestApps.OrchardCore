using Microsoft.AspNetCore.Mvc.Rendering;

namespace CrestApps.OrchardCore.ContactCenter.Reports.ViewModels;

/// <summary>
/// Represents the dimension filters available to Contact Center reports.
/// </summary>
public class ContactCenterReportFilterViewModel
{
    /// <summary>
    /// Gets or sets the selected queue identifier.
    /// </summary>
    public string QueueId { get; set; }

    /// <summary>
    /// Gets or sets the selected queue-group identifier.
    /// </summary>
    public string QueueGroupId { get; set; }

    /// <summary>
    /// Gets or sets the selected agent identifier.
    /// </summary>
    public string AgentId { get; set; }

    /// <summary>
    /// Gets or sets the selected campaign identifier.
    /// </summary>
    public string CampaignId { get; set; }

    /// <summary>
    /// Gets or sets the selected campaign group identifier.
    /// </summary>
    public string CampaignGroupId { get; set; }

    /// <summary>
    /// Gets or sets the selected channel.
    /// </summary>
    public string Channel { get; set; }

    /// <summary>
    /// Gets or sets the selected direction.
    /// </summary>
    public string Direction { get; set; }

    /// <summary>
    /// Gets or sets the selected activity source.
    /// </summary>
    public string ActivitySource { get; set; }

    /// <summary>
    /// Gets or sets the selected activity status.
    /// </summary>
    public string ActivityStatus { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the queue filter is displayed.
    /// </summary>
    public bool ShowQueueFilter { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the queue-group filter is displayed.
    /// </summary>
    public bool ShowQueueGroupFilter { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the agent filter is displayed.
    /// </summary>
    public bool ShowAgentFilter { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the campaign group filter is displayed.
    /// </summary>
    public bool ShowCampaignGroupFilter { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the campaign filter is displayed.
    /// </summary>
    public bool ShowCampaignFilter { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the channel filter is displayed.
    /// </summary>
    public bool ShowChannelFilter { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the direction filter is displayed.
    /// </summary>
    public bool ShowDirectionFilter { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the activity source filter is displayed.
    /// </summary>
    public bool ShowActivitySourceFilter { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the activity status filter is displayed.
    /// </summary>
    public bool ShowActivityStatusFilter { get; set; }

    /// <summary>
    /// Gets or sets the queue options.
    /// </summary>
    public IList<SelectListItem> Queues { get; set; } = [];

    /// <summary>
    /// Gets or sets the queue-group options.
    /// </summary>
    public IList<SelectListItem> QueueGroups { get; set; } = [];

    /// <summary>
    /// Gets or sets the agent options.
    /// </summary>
    public IList<SelectListItem> Agents { get; set; } = [];

    /// <summary>
    /// Gets or sets the campaign options.
    /// </summary>
    public IList<SelectListItem> Campaigns { get; set; } = [];

    /// <summary>
    /// Gets or sets the campaign group options.
    /// </summary>
    public IList<SelectListItem> CampaignGroups { get; set; } = [];

    /// <summary>
    /// Gets or sets the channel options.
    /// </summary>
    public IList<SelectListItem> Channels { get; set; } = [];

    /// <summary>
    /// Gets or sets the direction options.
    /// </summary>
    public IList<SelectListItem> Directions { get; set; } = [];

    /// <summary>
    /// Gets or sets the activity source options.
    /// </summary>
    public IList<SelectListItem> ActivitySources { get; set; } = [];

    /// <summary>
    /// Gets or sets the activity status options.
    /// </summary>
    public IList<SelectListItem> ActivityStatuses { get; set; } = [];
}
