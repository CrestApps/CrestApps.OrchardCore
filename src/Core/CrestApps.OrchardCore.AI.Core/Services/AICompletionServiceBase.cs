using CrestApps.AI.Prompting.Services;
using CrestApps.OrchardCore.AI.Models;

namespace CrestApps.OrchardCore.AI.Core.Services;

public abstract class AICompletionServiceBase
{
    protected readonly AIProviderOptions ProviderOptions;
    protected readonly IAITemplateService AITemplateService;

    protected AICompletionServiceBase(
        AIProviderOptions providerOptions,
        IAITemplateService aiTemplateService)
    {
        ProviderOptions = providerOptions;
        AITemplateService = aiTemplateService;
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
            var deploymentName = connection.GetChatDeploymentOrDefaultName();

            if (!string.IsNullOrEmpty(deploymentName))
            {
                return deploymentName;
            }
        }

        return provider.DefaultChatDeploymentName;
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

    protected async Task<string> GetSystemMessageAsync(AICompletionContext context)
    {
        var systemMessage = string.Empty;

        if (!string.IsNullOrEmpty(context.SystemMessage))
        {
            systemMessage = context.SystemMessage;
        }

        if (context.UserMarkdownInResponse)
        {
            var markdownInstruction = await AITemplateService.RenderAsync(AITemplateIds.UseMarkdownSyntax);
            systemMessage += Environment.NewLine + markdownInstruction;
        }

        return systemMessage;
    }
}
