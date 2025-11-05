using CrestApps.OrchardCore.AI.Models;

namespace CrestApps.OrchardCore.AI.Core.Services;

public abstract class AICompletionServiceBase
{
    protected readonly AIProviderOptions ProviderOptions;

    protected AICompletionServiceBase(AIProviderOptions providerOptions)
    {
        ProviderOptions = providerOptions;
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
            var deploymentName = connection.GetDefaultDeploymentName();

            if (!string.IsNullOrEmpty(deploymentName))
            {
                return deploymentName;
            }
        }

        return provider.DefaultDeploymentName;
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

    protected static string GetSystemMessage(AICompletionContext context)
    {
        var systemMessage = string.Empty;

        if (!string.IsNullOrEmpty(context.SystemMessage))
        {
            systemMessage = context.SystemMessage;
        }

        if (context.UserMarkdownInResponse)
        {
            systemMessage += Environment.NewLine + AIConstants.SystemMessages.UseMarkdownSyntax;
        }

        return systemMessage;
    }
}
