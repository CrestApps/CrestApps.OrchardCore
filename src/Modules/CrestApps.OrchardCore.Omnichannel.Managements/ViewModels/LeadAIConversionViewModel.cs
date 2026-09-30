using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace CrestApps.OrchardCore.Omnichannel.Managements.ViewModels;

/// <summary>
/// Represents the AI lead conversion options of an automatic inventory load.
/// </summary>
public class LeadAIConversionViewModel
{
    /// <summary>
    /// Gets or sets whether the options were on the submitted form.
    /// </summary>
    public bool Rendered { get; set; }

    /// <summary>
    /// Gets or sets whether the AI may convert a lead it qualified.
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// Gets or sets whether an AI conversion also creates an opportunity.
    /// </summary>
    public bool CreateOpportunity { get; set; }

    /// <summary>
    /// Gets or sets the opportunity type; empty uses the lead type's default.
    /// </summary>
    public string OpportunityContentType { get; set; }

    /// <summary>
    /// Gets or sets what qualified means for this load.
    /// </summary>
    public string QualificationGuidance { get; set; }

    /// <summary>
    /// Gets or sets the lead types, which decide when the options show.
    /// </summary>
    [BindNever]
    public string[] LeadContentTypes { get; set; } = [];

    /// <summary>
    /// Gets or sets the opportunity types to choose from.
    /// </summary>
    [BindNever]
    public IList<SelectListItem> OpportunityContentTypes { get; set; } = [];
}
