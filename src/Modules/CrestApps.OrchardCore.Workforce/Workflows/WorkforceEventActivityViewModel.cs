using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace CrestApps.OrchardCore.Workforce.Workflows;

/// <summary>
/// The editor of a Workforce workflow event.
/// </summary>
public class WorkforceEventActivityViewModel
{
    /// <summary>
    /// Gets or sets the teams the event starts the workflow for.
    /// </summary>
    public string[] TeamIds { get; set; } = [];

    /// <summary>
    /// Gets or sets the teams to choose from.
    /// </summary>
    [BindNever]
    public IList<SelectListItem> TeamOptions { get; set; } = [];
}
