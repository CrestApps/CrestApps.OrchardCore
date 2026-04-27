using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace CrestApps.OrchardCore.AI.ViewModels;

/// <summary>
/// Represents the view model for default AI deployment settings.
/// </summary>
public class DefaultAIDeploymentSettingsViewModel
{
    /// <summary>
    /// Gets or sets the default chat deployment name.
    /// </summary>
    public string DefaultChatDeploymentName { get; set; }

    /// <summary>
    /// Gets or sets the default utility deployment name.
    /// </summary>
    public string DefaultUtilityDeploymentName { get; set; }

    /// <summary>
    /// Gets or sets the default embedding deployment name.
    /// </summary>
    public string DefaultEmbeddingDeploymentName { get; set; }

    /// <summary>
    /// Gets or sets the default image deployment name.
    /// </summary>
    public string DefaultImageDeploymentName { get; set; }

    /// <summary>
    /// Gets or sets the default speech to text deployment name.
    /// </summary>
    public string DefaultSpeechToTextDeploymentName { get; set; }

    /// <summary>
    /// Gets or sets the default text to speech deployment name.
    /// </summary>
    public string DefaultTextToSpeechDeploymentName { get; set; }

    /// <summary>
    /// Gets or sets the default text to speech voice id.
    /// </summary>
    public string DefaultTextToSpeechVoiceId { get; set; }

    /// <summary>
    /// Gets or sets the chat deployments.
    /// </summary>
    [BindNever]
    public IEnumerable<SelectListItem> ChatDeployments { get; set; }

    /// <summary>
    /// Gets or sets the utility deployments.
    /// </summary>
    [BindNever]
    public IEnumerable<SelectListItem> UtilityDeployments { get; set; }

    /// <summary>
    /// Gets or sets the embedding deployments.
    /// </summary>
    [BindNever]
    public IEnumerable<SelectListItem> EmbeddingDeployments { get; set; }

    /// <summary>
    /// Gets or sets the image deployments.
    /// </summary>
    [BindNever]
    public IEnumerable<SelectListItem> ImageDeployments { get; set; }

    /// <summary>
    /// Gets or sets the speech to text deployments.
    /// </summary>
    [BindNever]
    public IEnumerable<SelectListItem> SpeechToTextDeployments { get; set; }

    /// <summary>
    /// Gets or sets the text to speech deployments.
    /// </summary>
    [BindNever]
    public IEnumerable<SelectListItem> TextToSpeechDeployments { get; set; }
}
