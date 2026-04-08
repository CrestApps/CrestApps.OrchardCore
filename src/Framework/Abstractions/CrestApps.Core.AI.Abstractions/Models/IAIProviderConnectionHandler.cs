namespace CrestApps.Core.AI.Models;

/// <summary>
/// Handles lifecycle events for AI provider connections, allowing customization
/// of connection initialization and data export behavior.
/// </summary>
public interface IAIProviderConnectionHandler
{
    /// <summary>
    /// Called when an AI provider connection is being initialized, allowing
    /// modification of the connection properties before persistence.
    /// </summary>
    /// <param name="context">The context containing the connection being initialized.</param>
    void Initializing(InitializingAIProviderConnectionContext context);

    /// <summary>
    /// Called when an AI provider connection is being exported, allowing
    /// sensitive data to be removed or transformed before serialization.
    /// </summary>
    /// <param name="context">The context containing the connection and export data.</param>
    void Exporting(ExportingAIProviderConnectionContext context);
}
