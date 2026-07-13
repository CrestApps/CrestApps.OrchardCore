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
    /// Gets or sets the campaign options.
    /// </summary>
    public IList<SelectListItem> Campaigns { get; set; } = [];

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
}
