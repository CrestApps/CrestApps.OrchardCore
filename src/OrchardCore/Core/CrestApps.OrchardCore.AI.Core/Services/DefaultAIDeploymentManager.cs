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

    protected override string GetGlobalDefaultId(AIDeploymentType type)
    {
        // In OrchardCore, settings are stored via ISiteService rather than IOptions.
        var settings = _siteService.GetSettingsAsync<DefaultAIDeploymentSettings>()
            .GetAwaiter().GetResult();

        return type switch
        {
            AIDeploymentType.Chat => settings.DefaultChatDeploymentId,
            AIDeploymentType.Utility => settings.DefaultUtilityDeploymentId,
            AIDeploymentType.Embedding => settings.DefaultEmbeddingDeploymentId,
            AIDeploymentType.Image => settings.DefaultImageDeploymentId,
            AIDeploymentType.SpeechToText => settings.DefaultSpeechToTextDeploymentId,
            AIDeploymentType.TextToSpeech => settings.DefaultTextToSpeechDeploymentId,
            _ => null,
        };
    }
}
