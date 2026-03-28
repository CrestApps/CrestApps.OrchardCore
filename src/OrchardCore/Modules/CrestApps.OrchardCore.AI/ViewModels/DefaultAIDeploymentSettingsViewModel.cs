using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace CrestApps.OrchardCore.AI.ViewModels;

public class DefaultAIDeploymentSettingsViewModel
{
    public string DefaultChatDeploymentId { get; set; }

    public string DefaultUtilityDeploymentId { get; set; }

    public string DefaultEmbeddingDeploymentId { get; set; }

    public string DefaultImageDeploymentId { get; set; }

    public string DefaultSpeechToTextDeploymentId { get; set; }

    public string DefaultTextToSpeechDeploymentId { get; set; }

    public string DefaultTextToSpeechVoiceId { get; set; }

    [BindNever]
    public IEnumerable<SelectListItem> ChatDeployments { get; set; }

    [BindNever]
    public IEnumerable<SelectListItem> UtilityDeployments { get; set; }

    [BindNever]
    public IEnumerable<SelectListItem> EmbeddingDeployments { get; set; }

    [BindNever]
    public IEnumerable<SelectListItem> ImageDeployments { get; set; }

    [BindNever]
    public IEnumerable<SelectListItem> SpeechToTextDeployments { get; set; }

    [BindNever]
    public IEnumerable<SelectListItem> TextToSpeechDeployments { get; set; }
}
