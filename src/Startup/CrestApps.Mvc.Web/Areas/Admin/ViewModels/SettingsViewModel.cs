using CrestApps.AI.Models;
using CrestApps.AI.Mcp.Models;
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

    public string DocumentIndexProfileName { get; set; }

    public int DocumentTopN { get; set; } = 3;

    public int DataSourceDefaultStrictness { get; set; } = AIDataSourceSettings.MinStrictness;

    public int DataSourceDefaultTopNDocuments { get; set; } = AIDataSourceSettings.MinTopNDocuments;

    public McpServerAuthenticationType McpServerAuthenticationType { get; set; } = McpServerAuthenticationType.OpenId;

    public string McpServerApiKey { get; set; }

    public bool McpServerRequireAccessPermission { get; set; } = true;

    // Default deployment settings.
    public string DefaultChatDeploymentName { get; set; }

    public string DefaultUtilityDeploymentName { get; set; }

    public string DefaultEmbeddingDeploymentName { get; set; }

    public string DefaultImageDeploymentName { get; set; }

    public string DefaultSpeechToTextDeploymentName { get; set; }

    public string DefaultTextToSpeechDeploymentName { get; set; }

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

    [BindNever]
    public IEnumerable<SelectListItem> DocumentIndexProfiles { get; set; } = [];
}
