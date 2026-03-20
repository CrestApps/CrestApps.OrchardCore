using CrestApps.OrchardCore.AI.Core.Models;
using CrestApps.OrchardCore.AI.Models;
using CrestApps.OrchardCore.Core.Services;
using CrestApps.OrchardCore.Services;
using Microsoft.Extensions.Logging;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.AI.Core.Services;

public sealed class DefaultAIDeploymentManager : NamedSourceCatalogManager<AIDeployment>, IAIDeploymentManager
{
    private readonly ISiteService _siteService;

    public DefaultAIDeploymentManager(
        INamedSourceCatalog<AIDeployment> deploymentStore,
        IEnumerable<ICatalogEntryHandler<AIDeployment>> handlers,
        ISiteService siteService,
        ILogger<DefaultAIDeploymentManager> logger)
        : base(deploymentStore, handlers, logger)
    {
        _siteService = siteService;
    }

    public async ValueTask<IEnumerable<AIDeployment>> GetAllAsync(string providerName, string connectionName)
    {
        var deployments = (await Catalog.GetAllAsync())
            .Where(x => x.ProviderName == providerName &&
            (x.ConnectionName.Equals(connectionName, StringComparison.OrdinalIgnoreCase) || string.Equals(x.ConnectionNameAlias ?? string.Empty, connectionName, StringComparison.OrdinalIgnoreCase)));

        foreach (var deployment in deployments)
        {
            await LoadAsync(deployment);
        }

        return deployments;
    }

    public async ValueTask<IEnumerable<AIDeployment>> GetByTypeAsync(AIDeploymentType type)
    {
        var deployments = (await Catalog.GetAllAsync())
            .Where(x => x.Type == type);

        foreach (var deployment in deployments)
        {
            await LoadAsync(deployment);
        }

        return deployments;
    }

    public async ValueTask<AIDeployment> GetDefaultAsync(string providerName, string connectionName, AIDeploymentType type)
    {
        var deployments = await GetAllAsync(providerName, connectionName);

        var candidates = deployments.Where(d => d.Type == type);

        return candidates.FirstOrDefault(d => d.IsDefault)
            ?? candidates.FirstOrDefault();
    }

    public async ValueTask<AIDeployment> ResolveAsync(AIDeploymentType type, string deploymentId = null, string providerName = null, string connectionName = null)
    {
        var result = await ResolveByTypeAsync(type, deploymentId, providerName, connectionName);

        // When resolving a Utility deployment and nothing was found,
        // fall back to the Chat deployment chain as a last resort.
        if (result == null && type == AIDeploymentType.Utility)
        {
            result = await ResolveByTypeAsync(AIDeploymentType.Chat, deploymentId: null, providerName, connectionName);
        }

        return result;
    }

    public async ValueTask<IEnumerable<AIDeployment>> GetAllByTypeAsync(AIDeploymentType type, string providerName = null)
    {
        var allDeployments = await GetAllAsync();

        var filtered = allDeployments.Where(d => d.Type == type);

        if (!string.IsNullOrEmpty(providerName))
        {
            filtered = filtered.Where(d => string.Equals(d.ProviderName, providerName, StringComparison.OrdinalIgnoreCase));
        }

        return filtered;
    }

    private async ValueTask<AIDeployment> ResolveByTypeAsync(AIDeploymentType type, string deploymentId, string providerName, string connectionName)
    {
        if (!string.IsNullOrEmpty(deploymentId))
        {
            var deployment = await FindByIdAsync(deploymentId);

            if (deployment != null)
            {
                return deployment;
            }
        }

        if (!string.IsNullOrEmpty(providerName) && !string.IsNullOrEmpty(connectionName))
        {
            var deployment = await GetDefaultAsync(providerName, connectionName, type);

            if (deployment != null)
            {
                return deployment;
            }
        }

        var globalDefaultId = await GetGlobalDefaultIdAsync(type);

        if (!string.IsNullOrEmpty(globalDefaultId))
        {
            return await FindByIdAsync(globalDefaultId);
        }

        return null;
    }

    private async ValueTask<string> GetGlobalDefaultIdAsync(AIDeploymentType type)
    {
        var settings = await _siteService.GetSettingsAsync<DefaultAIDeploymentSettings>();

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
