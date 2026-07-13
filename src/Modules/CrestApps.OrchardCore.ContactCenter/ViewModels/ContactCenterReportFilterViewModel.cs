using Microsoft.AspNetCore.Mvc.Rendering;

namespace CrestApps.OrchardCore.ContactCenter.ViewModels;

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
    /// Gets or sets the selected agent identifier.
    /// </summary>
    public string AgentId { get; set; }

    /// <summary>
    /// Gets or sets the selected campaign identifier.
    /// </summary>
    public string CampaignId { get; set; }

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
    /// Gets or sets a value indicating whether interaction-specific filters are displayed.
    /// </summary>
    public bool ShowInteractionFilters { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether activity-specific filters are displayed.
    /// </summary>
    public bool ShowActivityFilters { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether workforce-specific filters are displayed.
    /// </summary>
    public bool ShowWorkforceFilters { get; set; }

    /// <summary>
    /// Gets or sets the queue options.
    /// </summary>
    public IList<SelectListItem> Queues { get; set; } = [];

    /// <summary>
    /// Gets or sets the agent options.
    /// </summary>
    public IList<SelectListItem> Agents { get; set; } = [];

    /// <summary>
    /// Gets or sets the campaign options.
    /// </summary>
    public IList<SelectListItem> Campaigns { get; set; } = [];

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
