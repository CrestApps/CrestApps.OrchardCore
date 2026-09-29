using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace CrestApps.OrchardCore.Omnichannel.Managements.ViewModels;

/// <summary>
/// Represents the settings editor of a lead type.
/// </summary>
public class LeadPartSettingsViewModel
{
    /// <summary>
    /// Gets or sets the contact type a converted lead becomes.
    /// </summary>
    public string TargetContactContentType { get; set; }

    /// <summary>
    /// Gets or sets the opportunity type a conversion offers first.
    /// </summary>
    public string DefaultOpportunityContentType { get; set; }

    /// <summary>
    /// Gets or sets the contact types to choose from.
    /// </summary>
    [BindNever]
    public IList<SelectListItem> ContactContentTypes { get; set; } = [];

    /// <summary>
    /// Gets or sets the opportunity types to choose from.
    /// </summary>
    [BindNever]
    public IList<SelectListItem> OpportunityContentTypes { get; set; } = [];
}
