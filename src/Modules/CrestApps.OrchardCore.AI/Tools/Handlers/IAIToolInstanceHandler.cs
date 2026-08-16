namespace CrestApps.OrchardCore.AI.Tools.Handlers;

/// <summary>
/// Handles events raised for AI tool instances, such as removing sensitive data before an instance is exported.
/// </summary>
public interface IAIToolInstanceHandler
{
    /// <summary>
    /// Invoked while a tool instance is being exported so an implementer can remove sensitive data, such as API
    /// keys or other credentials, from the export payload before it is written to the deployment plan.
    /// </summary>
    /// <param name="context">The context describing the tool instance being exported.</param>
    void Exporting(ExportingAIToolInstanceContext context);
}
