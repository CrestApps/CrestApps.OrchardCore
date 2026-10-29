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

    /// <summary>
    /// Gets or sets how many seconds the AI waits for the workflow on a phone call before it gives up and tells the
    /// caller. <see langword="null"/> uses the call default (8 seconds); zero waits as long as the workflow takes.
    /// </summary>
    public int? TimeoutSeconds { get; set; }
}
