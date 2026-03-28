using System.Text.Json.Nodes;
using CrestApps.OrchardCore.AI.Models;

namespace CrestApps.OrchardCore.AI.Core.Models;

/// <summary>
/// Represents a deployment entry read from the application configuration (e.g., appsettings.json).
/// Used to define AI deployments for both connection-based and contained-connection providers.
/// </summary>
public sealed class AIDeploymentConfigurationEntry
{
    /// <summary>
    /// Gets or sets the deployment provider name for standalone configuration entries.
    /// </summary>
    public string ProviderName { get; set; }

    /// <summary>
    /// Gets or sets the deployment model name (e.g., "gpt-4o", "my-speech-to-text").
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets the deployment capability types (Chat, Utility, Embedding, Image, SpeechToText, TextToSpeech).
    /// </summary>
    public AIDeploymentType Type { get; set; }

    /// <summary>
    /// Gets or sets whether this deployment is the default for its type within its connection or provider.
    /// </summary>
    public bool IsDefault { get; set; }

    /// <summary>
    /// Gets or sets provider-specific properties for contained-connection deployments.
    /// These are usually flattened from top-level fields such as Endpoint, AuthenticationType, ApiKey, and IdentityId.
    /// </summary>
    public JsonObject Properties { get; set; }
}
