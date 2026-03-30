using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace CrestApps.Mvc.Web.Areas.Admin.ViewModels;

public sealed class SettingsViewModel
{
    // General AI settings.
    public bool EnablePreemptiveMemoryRetrieval { get; set; } = true;

    public int MaximumIterationsPerRequest { get; set; } = 10;

    public bool EnableDistributedCaching { get; set; } = true;

    public bool EnableOpenTelemetry { get; set; }

    // Default deployment settings.
    public string DefaultChatDeploymentId { get; set; }

    public string DefaultUtilityDeploymentId { get; set; }

    public string DefaultEmbeddingDeploymentId { get; set; }

    public string DefaultImageDeploymentId { get; set; }

    public string DefaultSpeechToTextDeploymentId { get; set; }

    public string DefaultTextToSpeechDeploymentId { get; set; }

    public string DefaultTextToSpeechVoiceId { get; set; }

    // Dropdown items — never bound from form data.
    [BindNever]
    public IEnumerable<SelectListItem> ChatDeployments { get; set; } = [];

    [BindNever]
    public IEnumerable<SelectListItem> UtilityDeployments { get; set; } = [];

    [BindNever]
    public IEnumerable<SelectListItem> EmbeddingDeployments { get; set; } = [];

    [BindNever]
    public IEnumerable<SelectListItem> ImageDeployments { get; set; } = [];

    [BindNever]
    public IEnumerable<SelectListItem> SpeechToTextDeployments { get; set; } = [];

    [BindNever]
    public IEnumerable<SelectListItem> TextToSpeechDeployments { get; set; } = [];
}
