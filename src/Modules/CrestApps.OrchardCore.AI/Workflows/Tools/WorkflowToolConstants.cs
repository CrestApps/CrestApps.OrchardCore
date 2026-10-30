namespace CrestApps.OrchardCore.AI.Workflows.Tools;

/// <summary>
/// Names used by the <c>Workflow</c> tool instance source.
/// </summary>
public static class WorkflowToolConstants
{
    /// <summary>
    /// The registered name of the source, stored on every instance created from it.
    /// </summary>
    public const string SourceName = "Workflow";

    /// <summary>
    /// The parameter binding that passes a value to the workflow as one of its arguments.
    /// </summary>
    public const string ArgumentBinding = "Argument";

    /// <summary>
    /// The free-form argument the model fills when the instance declares no parameters.
    /// </summary>
    public const string RequestArgument = "request";
}
