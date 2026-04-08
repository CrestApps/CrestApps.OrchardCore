using CrestApps.Core.AI.Copilot.Models;
using CrestApps.Core.AI.Mcp.Models;
using CrestApps.Core.AI.Models;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace CrestApps.Core.Mvc.Web.Areas.Admin.ViewModels;

public sealed class SettingsViewModel
{
    public bool EnableAIUsageTracking { get; set; }

    public bool EnablePreemptiveMemoryRetrieval { get; set; } = true;

    public int MaximumIterationsPerRequest { get; set; } = 10;

    public bool EnableDistributedCaching { get; set; } = true;

    public bool EnableOpenTelemetry { get; set; }

    public bool DefaultOrchestratorEnablePreemptiveRag { get; set; } = true;

    public ChatMode ChatInteractionChatMode { get; set; }

    public string MemoryIndexProfileName { get; set; }

    public int MemoryTopN { get; set; } = 5;

    public bool EnableUserMemoryByDefault { get; set; } = true;

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

    // Copilot settings.
    public CopilotAuthenticationType CopilotAuthenticationType { get; set; }

    public string CopilotClientId { get; set; }

    public string CopilotClientSecret { get; set; }

    public bool CopilotHasSecret { get; set; }

    public string CopilotProviderType { get; set; }

    public string CopilotBaseUrl { get; set; }

    public string CopilotApiKey { get; set; }

    public bool CopilotHasApiKey { get; set; }

    public string CopilotWireApi { get; set; } = "completions";

    public string CopilotDefaultModel { get; set; }

    public string CopilotAzureApiVersion { get; set; }

    // Pagination settings.
    public int AdminPageSize { get; set; } = 25;

    public string AdminWidgetProfileId { get; set; }

    public string AdminWidgetPrimaryColor { get; set; }

    [BindNever]
    public string CopilotCallbackUrl { get; set; }

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
    public IEnumerable<SelectListItem> ChatInteractionModes { get; set; } = [];

    [BindNever]
    public IEnumerable<SelectListItem> DocumentIndexProfiles { get; set; } = [];

    [BindNever]
    public IEnumerable<SelectListItem> MemoryIndexProfiles { get; set; } = [];

    [BindNever]
    public IEnumerable<SelectListItem> AdminWidgetProfiles { get; set; } = [];
}
