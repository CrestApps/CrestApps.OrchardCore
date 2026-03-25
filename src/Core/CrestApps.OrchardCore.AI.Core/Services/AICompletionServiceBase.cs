using CrestApps.AI.Prompting.Services;
using CrestApps.OrchardCore.AI.Models;

namespace CrestApps.OrchardCore.AI.Core.Services;

public abstract class AICompletionServiceBase
{
    protected readonly AIProviderOptions ProviderOptions;
    protected readonly IAITemplateService AITemplateService;
    protected readonly IAIDeploymentManager DeploymentResolver;

    protected AICompletionServiceBase(
        AIProviderOptions providerOptions,
        IAITemplateService aiTemplateService)
    {
        ProviderOptions = providerOptions;
        AITemplateService = aiTemplateService;
    }

    protected AICompletionServiceBase(
        AIProviderOptions providerOptions,
        IAITemplateService aiTemplateService,
        IAIDeploymentManager deploymentResolver)
        : this(providerOptions, aiTemplateService)
    {
        DeploymentResolver = deploymentResolver;
    }

    protected virtual string GetDefaultConnectionName(AIProvider provider, string connectionName)
    {
        if (!string.IsNullOrEmpty(connectionName))
        {
            return connectionName;
        }

        return provider.DefaultConnectionName;
    }

    protected virtual string GetDefaultDeploymentName(AIProvider provider, string connectionName)
    {
        if (connectionName is not null && provider.Connections.TryGetValue(connectionName, out var connection))
        {
#pragma warning disable CS0618 // Obsolete deployment name methods retained for backward compatibility
            var deploymentName = connection.GetChatDeploymentOrDefaultName();
#pragma warning restore CS0618

            if (!string.IsNullOrEmpty(deploymentName))
            {
                return deploymentName;
            }
        }

#pragma warning disable CS0618 // Obsolete deployment name fields retained for backward compatibility
        return provider.DefaultChatDeploymentName;
#pragma warning restore CS0618
    }

    /// <summary>
    /// Resolves a deployment name and connection name using the <see cref="IAIDeploymentManager"/>
    /// with fallback to the legacy dictionary-based resolution.
    /// </summary>
    protected virtual async ValueTask<(string DeploymentName, string ConnectionName)> ResolveDeploymentAsync(
        AIDeploymentType type,
        AIProvider provider,
        string providerName,
        string connectionName,
        string deploymentId = null)
    {
        if (DeploymentResolver != null)
        {
            var deployment = await DeploymentResolver.ResolveOrDefaultAsync(
                type,
                deploymentId: deploymentId,
                clientName: providerName,
                connectionName: connectionName);

            if (deployment != null)
            {
                return (deployment.Name, deployment.ConnectionName ?? connectionName);
            }
        }

        // Fall back to legacy dictionary-based resolution.
        var legacyName = GetDefaultDeploymentName(provider, connectionName);

        return (legacyName, connectionName);
    }

    protected static int GetTotalMessagesToSkip(int totalMessages, int pastMessageCount)
    {
        if (pastMessageCount > 0 && totalMessages > pastMessageCount)
        {
            return totalMessages - pastMessageCount;
        }

        return 0;
    }

    protected virtual Task<AIDeployment> GetDeploymentAsync(AICompletionContext content)
    {
        return Task.FromResult<AIDeployment>(null);
    }
}
