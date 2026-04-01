using CrestApps.AI.Models;
using CrestApps.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.AI.Core.Services;

/// <summary>
/// OrchardCore-specific AIDeploymentManager that reads global default deployment
/// settings from ISiteService instead of IOptions.
/// </summary>
public sealed class DefaultAIDeploymentManager : CrestApps.AI.Services.DefaultAIDeploymentManager
{
    private readonly ISiteService _siteService;
    public DefaultAIDeploymentManager(
        INamedSourceCatalog<AIDeployment> deploymentStore,
        IEnumerable<ICatalogEntryHandler<AIDeployment>> handlers,
        IOptionsMonitor<DefaultAIDeploymentSettings> deploymentSettings,
        ISiteService siteService,
        ILogger<DefaultAIDeploymentManager> logger)
    : base(deploymentStore, handlers, deploymentSettings, logger)
    {
        _siteService = siteService;
    }

    protected override async ValueTask<string> GetGlobalDefaultSelectorAsync(AIDeploymentType type)
    {
        var settings = await _siteService.GetSettingsAsync<DefaultAIDeploymentSettings>();

        return type switch
        {
            AIDeploymentType.Chat => settings.DefaultChatDeploymentName,
            AIDeploymentType.Utility => settings.DefaultUtilityDeploymentName,
            AIDeploymentType.Embedding => settings.DefaultEmbeddingDeploymentName,
            AIDeploymentType.Image => settings.DefaultImageDeploymentName,
            AIDeploymentType.SpeechToText => settings.DefaultSpeechToTextDeploymentName,
            AIDeploymentType.TextToSpeech => settings.DefaultTextToSpeechDeploymentName,
            _ => null,
        };
    }
}
