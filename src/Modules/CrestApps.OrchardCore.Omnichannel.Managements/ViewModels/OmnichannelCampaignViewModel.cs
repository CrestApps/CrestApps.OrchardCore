using Microsoft.AspNetCore.Mvc.Rendering;

namespace CrestApps.OrchardCore.Omnichannel.Managements.ViewModels;

/// <summary>
/// Represents the view model for omnichannel campaign.
/// </summary>
public class OmnichannelCampaignViewModel
{
    /// <summary>
    /// Gets or sets the display text.
    /// </summary>
    public string DisplayText { get; set; }

    /// <summary>
    /// Gets or sets the description.
    /// </summary>
    public string Description { get; set; }

    /// <summary>
    /// Gets or sets the selected campaign group identifier.
    /// </summary>
    public string CampaignGroupId { get; set; }

    /// <summary>
    /// Gets or sets the available campaign groups.
    /// </summary>
    public IList<SelectListItem> CampaignGroups { get; set; } = [];
}
