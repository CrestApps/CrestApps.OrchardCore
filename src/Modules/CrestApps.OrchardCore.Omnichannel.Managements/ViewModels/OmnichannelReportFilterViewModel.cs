using Microsoft.AspNetCore.Mvc.Rendering;

namespace CrestApps.OrchardCore.Omnichannel.Managements.ViewModels;

/// <summary>
/// Represents the dimension filters available to Omnichannel reports.
/// </summary>
public class OmnichannelReportFilterViewModel
{
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
    /// Gets or sets the selected source.
    /// </summary>
    public string Source { get; set; }

    /// <summary>
    /// Gets or sets the selected status.
    /// </summary>
    public string Status { get; set; }

    /// <summary>
    /// Gets or sets the selected disposition identifiers. Several can be chosen together.
    /// </summary>
    public string[] DispositionIds { get; set; } = [];

    /// <summary>
    /// Gets or sets the one-based page of rows to show, for a report that pages its rows.
    /// </summary>
    public int Page { get; set; } = 1;

    /// <summary>
    /// Gets or sets a value indicating whether the report pages its rows, so the page picker is shown.
    /// </summary>
    public bool ShowPage { get; set; }

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
    /// Gets or sets the source options.
    /// </summary>
    public IList<SelectListItem> Sources { get; set; } = [];

    /// <summary>
    /// Gets or sets the status options.
    /// </summary>
    public IList<SelectListItem> Statuses { get; set; } = [];

    /// <summary>
    /// Gets or sets the disposition options.
    /// </summary>
    public IList<SelectListItem> Dispositions { get; set; } = [];
}
