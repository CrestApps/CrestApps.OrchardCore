namespace CrestApps.OrchardCore.AI.Core.Models;

/// <summary>
/// Global site settings for default AI deployments by type.
/// Configurable under Settings >> Artificial Intelligence >> Default Deployments.
/// These provide the ultimate fallback when a specific deployment of a given type
/// is not configured at the connection or profile level.
/// </summary>
public sealed class DefaultAIDeploymentSettings
{
    /// <summary>
    /// Gets or sets the default chat deployment identifier.
    /// Used globally for chat sessions and chat interactions when no specific chat deployment is configured.
    /// </summary>
    public string DefaultChatDeploymentId { get; set; }

    /// <summary>
    /// Gets or sets the default utility deployment identifier.
    /// Used globally for auxiliary tasks such as planning, intent detection,
    /// query rewriting, and chart generation when no specific utility deployment is configured.
    /// </summary>
    public string DefaultUtilityDeploymentId { get; set; }

    /// <summary>
    /// Gets or sets the default embedding deployment identifier.
    /// Used globally for embedding generation in document indexing, data sources,
    /// and vector search when no specific embedding deployment is configured.
    /// </summary>
    public string DefaultEmbeddingDeploymentId { get; set; }

    /// <summary>
    /// Gets or sets the default image generation deployment identifier.
    /// Used globally for image generation when no specific image deployment is configured.
    /// </summary>
    public string DefaultImageDeploymentId { get; set; }

    /// <summary>
    /// Gets or sets the default speech-to-text deployment identifier.
    /// Used globally for speech-to-text transcription when no specific deployment is configured.
    /// </summary>
    public string DefaultSpeechToTextDeploymentId { get; set; }

    /// <summary>
    /// Gets or sets the default text-to-speech deployment identifier.
    /// Used globally for text-to-speech synthesis when no specific deployment is configured.
    /// </summary>
    public string DefaultTextToSpeechDeploymentId { get; set; }

    /// <summary>
    /// Gets or sets the default voice identifier for text-to-speech synthesis.
    /// When a profile does not specify a voice, this value is used as the fallback.
    /// </summary>
    public string DefaultTextToSpeechVoiceId { get; set; }
}
