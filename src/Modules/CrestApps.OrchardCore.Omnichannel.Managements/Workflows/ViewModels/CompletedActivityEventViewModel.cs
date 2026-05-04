using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace CrestApps.OrchardCore.Omnichannel.Managements.Workflows.ViewModels;

/// <summary>
/// Represents the view model for completed activity event.
/// </summary>
public class CompletedActivityEventViewModel
{
    /// <summary>
    /// Gets or sets the campaign id.
    /// </summary>
    public string CampaignId { get; set; }

    /// <summary>
    /// Gets or sets the campaigns.
    /// </summary>
    [BindNever]
    public IEnumerable<SelectListItem> Campaigns { get; set; }
}
