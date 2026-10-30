namespace CrestApps.OrchardCore.AI.Workflows.Tools;

/// <summary>
/// The settings of a <c>Workflow</c> tool instance.
/// </summary>
public sealed class WorkflowToolSettings
{
    /// <summary>
    /// Gets or sets the workflow type the tool runs. It starts with the <c>AI Tool Called</c> event.
    /// </summary>
    public string WorkflowTypeId { get; set; }
}
