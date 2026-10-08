using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace CrestApps.OrchardCore.Omnichannel.Managements.ViewModels;

/// <summary>
/// Represents the settings editor of an opportunity type.
/// </summary>
public class OpportunityPartSettingsViewModel
{
    /// <summary>
    /// Gets or sets the identifiers of the stages the type moves through.
    /// </summary>
    public string[] StageIds { get; set; } = [];

    /// <summary>
    /// Gets or sets every stage to choose from.
    /// </summary>
    [BindNever]
    public IList<SelectListItem> Stages { get; set; } = [];
}
