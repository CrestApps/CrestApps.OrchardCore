using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace CrestApps.OrchardCore.AI.ViewModels;

public class DefaultAIDeploymentSettingsViewModel
{
    public string DefaultChatDeploymentName { get; set; }

    public string DefaultUtilityDeploymentName { get; set; }

    public string DefaultEmbeddingDeploymentName { get; set; }

    public string DefaultImageDeploymentName { get; set; }

    public string DefaultSpeechToTextDeploymentName { get; set; }

    public string DefaultTextToSpeechDeploymentName { get; set; }

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
