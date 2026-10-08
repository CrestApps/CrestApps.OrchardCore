using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace CrestApps.OrchardCore.Omnichannel.Managements.ViewModels;

/// <summary>
/// Represents the export options of a lead type.
/// </summary>
public class LeadExportOptionsViewModel
{
    /// <summary>
    /// Gets or sets whether the options were on the submitted form.
    /// </summary>
    public bool Rendered { get; set; }

    /// <summary>
    /// Gets or sets whether converted leads are left out of the export.
    /// </summary>
    public bool ExcludeConvertedLeads { get; set; } = true;

    /// <summary>
    /// Gets or sets the lead types, which decide when the option shows.
    /// </summary>
    [BindNever]
    public string[] LeadContentTypes { get; set; } = [];
}
