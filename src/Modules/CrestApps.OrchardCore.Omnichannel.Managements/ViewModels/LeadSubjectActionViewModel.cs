using CrestApps.OrchardCore.Omnichannel.Core.Models;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace CrestApps.OrchardCore.Omnichannel.Managements.ViewModels;

/// <summary>
/// Represents the lead settings of a subject action.
/// </summary>
public class LeadSubjectActionViewModel
{
    /// <summary>
    /// Gets or sets the status the action moves a lead to.
    /// </summary>
    public string SetLeadStatusId { get; set; }

    /// <summary>
    /// Gets or sets how the Convert lead action chooses the account.
    /// </summary>
    public LeadConversionAccountMode AccountMode { get; set; } = LeadConversionAccountMode.Automatic;

    /// <summary>
    /// Gets or sets whether the Convert lead action creates an opportunity.
    /// </summary>
    public bool CreateOpportunity { get; set; }

    /// <summary>
    /// Gets or sets the opportunity type the Convert lead action creates.
    /// </summary>
    public string OpportunityContentType { get; set; }

    /// <summary>
    /// Gets or sets what the Convert lead action does with the lead's other open activities.
    /// </summary>
    public LeadOpenActivityMode OpenActivities { get; set; } = LeadOpenActivityMode.Move;

    /// <summary>
    /// Gets or sets the statuses to choose from.
    /// </summary>
    [BindNever]
    public IList<SelectListItem> LeadStatuses { get; set; } = [];

    /// <summary>
    /// Gets or sets the opportunity types to choose from.
    /// </summary>
    [BindNever]
    public IList<SelectListItem> OpportunityContentTypes { get; set; } = [];

    /// <summary>
    /// Gets or sets the account choices.
    /// </summary>
    [BindNever]
    public IList<SelectListItem> AccountModes { get; set; } = [];
}
