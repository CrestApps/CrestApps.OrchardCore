using CrestApps.AI.Models;
using CrestApps.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CrestApps.AI.Services;

public class DefaultAIDeploymentManager : NamedSourceCatalogManager<AIDeployment>, IAIDeploymentManager
{
    private readonly IOptionsMonitor<DefaultAIDeploymentSettings> _deploymentSettings;

    public DefaultAIDeploymentManager(
        INamedSourceCatalog<AIDeployment> deploymentStore,
        IEnumerable<ICatalogEntryHandler<AIDeployment>> handlers,
        IOptionsMonitor<DefaultAIDeploymentSettings> deploymentSettings,
        ILogger<DefaultAIDeploymentManager> logger)
        : base(deploymentStore, handlers, logger)
    {
        _deploymentSettings = deploymentSettings;
    }

    public async ValueTask<IEnumerable<AIDeployment>> GetAllAsync(string clientName, string connectionName)
    {
        var deployments = (await Catalog.GetAllAsync())
            .Where(x => string.Equals(x.ClientName, clientName, StringComparison.OrdinalIgnoreCase) &&
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

    public async ValueTask<AIDeployment> GetDefaultAsync(string clientName, string connectionName, AIDeploymentType type)
    {
        var deployments = await GetAllAsync(clientName, connectionName);

        var candidates = deployments.Where(d => d.Type == type);

        return candidates.FirstOrDefault(d => d.IsDefault)
            ?? candidates.FirstOrDefault();
    }

    public async ValueTask<AIDeployment> ResolveOrDefaultAsync(AIDeploymentType type, string deploymentId = null, string clientName = null, string connectionName = null)
    {
        return await ResolveByTypeAsync(type, deploymentId, clientName, connectionName);
    }

    public async ValueTask<IEnumerable<AIDeployment>> GetAllByTypeAsync(AIDeploymentType type, string clientName = null)
    {
        var allDeployments = await GetAllAsync();

        var filtered = allDeployments.Where(d => d.Type == type);

        if (!string.IsNullOrEmpty(clientName))
        {
            filtered = filtered.Where(d => string.Equals(d.ClientName, clientName, StringComparison.OrdinalIgnoreCase));
        }

        return filtered;
    }

    private async ValueTask<AIDeployment> ResolveByTypeAsync(AIDeploymentType type, string deploymentId, string clientName, string connectionName)
    {
        if (!string.IsNullOrEmpty(deploymentId))
        {
            var deployment = await FindByIdAsync(deploymentId);

            if (deployment != null)
            {
                return deployment;
            }
        }

        if (!string.IsNullOrEmpty(clientName) && !string.IsNullOrEmpty(connectionName))
        {
            var deployment = await GetDefaultAsync(clientName, connectionName, type);

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

    protected virtual ValueTask<string> GetGlobalDefaultIdAsync(AIDeploymentType type)
    {
        var settings = _deploymentSettings.CurrentValue;

        var result = type switch
        {
            AIDeploymentType.Chat => settings.DefaultChatDeploymentId,
            AIDeploymentType.Utility => settings.DefaultUtilityDeploymentId,
            AIDeploymentType.Embedding => settings.DefaultEmbeddingDeploymentId,
            AIDeploymentType.Image => settings.DefaultImageDeploymentId,
            AIDeploymentType.SpeechToText => settings.DefaultSpeechToTextDeploymentId,
            AIDeploymentType.TextToSpeech => settings.DefaultTextToSpeechDeploymentId,
            _ => null,
        };

        return new ValueTask<string>(result);
    }
}
