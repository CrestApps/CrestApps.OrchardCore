using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace CrestApps.OrchardCore.Omnichannel.Managements.ViewModels;

/// <summary>
/// Represents the lead settings of a disposition.
/// </summary>
public class LeadDispositionViewModel
{
    /// <summary>
    /// Gets or sets the status the disposition moves a lead to.
    /// </summary>
    public string SetLeadStatusId { get; set; }

    /// <summary>
    /// Gets or sets the statuses to choose from.
    /// </summary>
    [BindNever]
    public IList<SelectListItem> LeadStatuses { get; set; } = [];
}
