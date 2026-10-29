using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace CrestApps.OrchardCore.AI.Workflows.Tools;

/// <summary>
/// The editor of a <c>Workflow</c> tool instance.
/// </summary>
public class WorkflowToolInstanceViewModel
{
    /// <summary>
    /// Gets or sets the workflow the tool runs.
    /// </summary>
    public string WorkflowTypeId { get; set; }

    /// <summary>
    /// Gets or sets how many seconds a phone call waits for the workflow; empty uses the default.
    /// </summary>
    public int? TimeoutSeconds { get; set; }

    /// <summary>
    /// Gets or sets the workflows that start with the <c>AI Tool Called</c> event.
    /// </summary>
    [BindNever]
    public IList<SelectListItem> WorkflowTypes { get; set; } = [];
}
