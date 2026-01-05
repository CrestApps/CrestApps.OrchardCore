using CrestApps.OrchardCore.AI.Core.Handlers;
using CrestApps.OrchardCore.AI.Models;

namespace CrestApps.OrchardCore.AI.Handlers;

/// <summary>
/// Handler for initializing speech-to-text AI provider connections.
/// Sets connection name, provider name, and deployment ID in the context values.
/// </summary>
public sealed class SpeechToTextAIProviderConnectionHandler : AIProviderConnectionHandlerBase
{
    public override void Initializing(InitializingAIProviderConnectionContext context)
    {
        // Get the connection type from the Connection directly
        var connectionType = context.Connection.Type;

        // Only process if this is a SpeechToText connection
        if (connectionType != AIProviderConnectionType.SpeechToText)
        {
            return;
        }

        // The connection values should already have the basic properties set
        // We ensure that ProviderName is available in the context
        if (!context.Values.ContainsKey("ProviderName"))
        {
            context.Values["ProviderName"] = context.Connection.ProviderName;
        }

        // Set the connection name if not already set
        if (!context.Values.ContainsKey("ConnectionName"))
        {
            context.Values["ConnectionName"] = context.Connection.Name;
        }

        // Get deployment from connection if available
        var deployment = context.Connection.DefaultDeploymentName;
        if (!string.IsNullOrEmpty(deployment) && !context.Values.ContainsKey("DeploymentId"))
        {
            context.Values["DeploymentId"] = deployment;
        }
    }
}
