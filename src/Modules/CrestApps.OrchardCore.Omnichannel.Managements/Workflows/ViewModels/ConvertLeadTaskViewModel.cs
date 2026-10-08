using CrestApps.OrchardCore.Omnichannel.Core.Models;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace CrestApps.OrchardCore.Omnichannel.Managements.Workflows.ViewModels;

/// <summary>
/// Represents the editor of the Convert Lead workflow task.
/// </summary>
public class ConvertLeadTaskViewModel
{
    /// <summary>
    /// Gets or sets the Liquid expression that resolves the lead's content item id.
    /// </summary>
    public string LeadContentItemId { get; set; }

    /// <summary>
    /// Gets or sets how the contact gets an account.
    /// </summary>
    public LeadConversionAccountMode AccountMode { get; set; }

    /// <summary>
    /// Gets or sets whether the conversion also creates an opportunity.
    /// </summary>
    public bool CreateOpportunity { get; set; }

    /// <summary>
    /// Gets or sets the opportunity type; empty uses the lead type's default.
    /// </summary>
    public string OpportunityContentType { get; set; }

    /// <summary>
    /// Gets or sets what happens to the lead's other open activities.
    /// </summary>
    public LeadOpenActivityMode OpenActivities { get; set; }

    /// <summary>
    /// Gets or sets the opportunity types to choose from.
    /// </summary>
    [BindNever]
    public IList<SelectListItem> OpportunityContentTypes { get; set; } = [];
}
