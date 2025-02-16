using CrestApps.OrchardCore.AI.Core.Models;
using CrestApps.OrchardCore.AI.Models;

namespace CrestApps.OrchardCore.AI.Core.Services;

public abstract class AICompletionServiceBase
{
    protected readonly AIProviderOptions ProviderOptions;

    protected AICompletionServiceBase(AIProviderOptions providerOptions)
    {
        ProviderOptions = providerOptions;
    }

    protected virtual string GetDefaultConnectionName(AIProvider provider)
    {
        return provider.DefaultConnectionName;
    }

    protected virtual string GetDefaultDeploymentName(AIProvider provider)
    {
        return provider.DefaultDeploymentName;
    }

    protected async Task<Tuple<AIProviderConnection, string>> GetConnectionAsync(AICompletionContext context, string providerName)
    {
        string deploymentName = null;

        if (ProviderOptions.Providers.TryGetValue(providerName, out var provider))
        {
            var connectionName = GetDefaultConnectionName(provider);

            deploymentName = GetDefaultDeploymentName(provider);

            var deployment = await GetDeploymentAsync(context);

            if (deployment is not null)
            {
                connectionName = deployment.ConnectionName;
                deploymentName = deployment.Name;
            }

            if (!string.IsNullOrEmpty(connectionName) && provider.Connections.TryGetValue(connectionName, out var connectionProperties))
            {
                return new Tuple<AIProviderConnection, string>(connectionProperties, deploymentName);
            }
        }

        return new Tuple<AIProviderConnection, string>(null, deploymentName);
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

    protected static string GetSystemMessage(AICompletionContext context, AIProfileMetadata metadata)
    {
        var systemMessage = string.Empty;

        if (!string.IsNullOrEmpty(context.SystemMessage))
        {
            systemMessage = context.SystemMessage;
        }
        else if (!string.IsNullOrEmpty(metadata.SystemMessage))
        {
            systemMessage = metadata.SystemMessage;
        }

        if (context.UserMarkdownInResponse)
        {
            systemMessage += Environment.NewLine + AIConstants.SystemMessages.UseMarkdownSyntax;
        }

        return systemMessage;
    }
}
