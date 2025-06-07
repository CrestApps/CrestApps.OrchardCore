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

    protected virtual string GetDefaultConnectionName(AIProvider provider, AIProfile profile)
    {
        if (!string.IsNullOrEmpty(profile.ConnectionName))
        {
            return profile.ConnectionName;
        }

        return provider.DefaultConnectionName;
    }

    protected virtual string GetDefaultDeploymentName(AIProvider provider)
    {
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
