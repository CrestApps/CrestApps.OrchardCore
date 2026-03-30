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

    public async ValueTask<IEnumerable<AIDeployment>> GetAllAsync(string clientName, string connectionName)
    {
        var deployments = (await Catalog.GetAllAsync())
            .Where(x => string.Equals(x.ClientName, clientName, StringComparison.OrdinalIgnoreCase) &&
            (string.Equals(x.ConnectionName ?? string.Empty, connectionName, StringComparison.OrdinalIgnoreCase) ||
             string.Equals(x.ConnectionNameAlias ?? string.Empty, connectionName, StringComparison.OrdinalIgnoreCase)));

        foreach (var deployment in deployments)
        {
            await LoadAsync(deployment);
        }

        return deployments;
    }

    public async ValueTask<IEnumerable<AIDeployment>> GetByTypeAsync(AIDeploymentType type)
    {
        var deployments = (await Catalog.GetAllAsync())
            .Where(x => x.SupportsType(type));

        foreach (var deployment in deployments)
        {
            await LoadAsync(deployment);
        }

        return deployments;
    }

    public async ValueTask<AIDeployment> GetDefaultAsync(string clientName, string connectionName, AIDeploymentType type)
    {
        var deployments = await GetAllAsync(clientName, connectionName);

        var candidates = deployments.Where(d => d.SupportsType(type));

        return candidates.FirstOrDefault(d => d.IsDefault)
            ?? candidates.FirstOrDefault();
    }

    public ValueTask<AIDeployment> ResolveOrDefaultAsync(AIDeploymentType type, string deploymentName = null, string clientName = null, string connectionName = null)
    {
        return ResolveByTypeAsync(type, deploymentName, clientName, connectionName);
    }

    public async ValueTask<IEnumerable<AIDeployment>> GetAllByTypeAsync(AIDeploymentType type, string clientName = null)
    {
        var allDeployments = await GetAllAsync();

        var filtered = allDeployments.Where(d => d.SupportsType(type));

        if (!string.IsNullOrEmpty(clientName))
        {
            filtered = filtered.Where(d => string.Equals(d.ClientName, clientName, StringComparison.OrdinalIgnoreCase));
        }

        return filtered;
    }

    private async ValueTask<AIDeployment> ResolveByTypeAsync(AIDeploymentType type, string deploymentName, string clientName, string connectionName)
    {
        if (!string.IsNullOrEmpty(deploymentName))
        {
            var deployment = await FindBySelectorAsync(deploymentName);

            if (deployment != null)
            {
                return deployment;
            }
        }

        var globalDefaultId = await GetGlobalDefaultSelectorAsync(type);

        if (!string.IsNullOrEmpty(globalDefaultId))
        {
            var deployment = await FindBySelectorAsync(globalDefaultId);

            if (deployment != null)
            {
                return deployment;
            }
        }

        return await GetFirstMatchingDeploymentAsync(type, clientName, connectionName);
    }

    private async ValueTask<AIDeployment> GetFirstMatchingDeploymentAsync(AIDeploymentType type, string clientName, string connectionName)
    {
        var deployments = await GetAllAsync();

        return deployments.FirstOrDefault(deployment =>
        {
            if (!deployment.SupportsType(type))
            {
                return false;
            }

            if (!string.IsNullOrEmpty(clientName) &&
                !string.Equals(deployment.ClientName, clientName, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            if (string.IsNullOrEmpty(connectionName))
            {
                return true;
            }

            return string.Equals(deployment.ConnectionName ?? string.Empty, connectionName, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(deployment.ConnectionNameAlias ?? string.Empty, connectionName, StringComparison.OrdinalIgnoreCase);
        });
    }

    private async ValueTask<AIDeployment> FindBySelectorAsync(string selector)
    {
        var deployment = await FindByIdAsync(selector);

        if (deployment != null)
        {
            return deployment;
        }

        return await FindByNameAsync(selector);
    }

    private async ValueTask<string> GetGlobalDefaultSelectorAsync(AIDeploymentType type)
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
