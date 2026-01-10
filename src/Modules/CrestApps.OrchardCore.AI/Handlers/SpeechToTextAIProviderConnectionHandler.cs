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
        // Get the AI profile from the connection's associated profile
        // This handler ensures that speech-to-text metadata is properly initialized
        // when the connection is used in the AI Chat hub

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
        var deployment = context.Connection.DefaultSpeechToTextDeploymentName;

        if (!string.IsNullOrEmpty(deployment) && !context.Values.ContainsKey("DeploymentId"))
        {
            context.Values["DeploymentId"] = deployment;
        }
    }
}
