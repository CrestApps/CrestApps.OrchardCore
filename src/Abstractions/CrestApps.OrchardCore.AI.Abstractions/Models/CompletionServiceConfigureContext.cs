using Microsoft.Extensions.AI;

namespace CrestApps.OrchardCore.AI.Models;

public sealed class CompletionServiceConfigureContext
{
    public string ProviderName { get; set; }

    public string ImplemenationName { get; set; }

    public string DeploymentName { get; set; }

    public bool IsStreaming { get; set; }

    public ChatOptions ChatOptions { get; }

    public readonly AICompletionContext CompletionContext;

    public bool IsFunctionInvocationSupported { get; }

    public Dictionary<string, object> AdditionalProperties { get; set; }

    public CompletionServiceConfigureContext(
        ChatOptions chatOptions,
        AICompletionContext completionContext,
        bool isFunctionInvocationSupported)
    {
        ArgumentNullException.ThrowIfNull(chatOptions);
        ArgumentNullException.ThrowIfNull(completionContext);

        ChatOptions = chatOptions;
        CompletionContext = completionContext;
        IsFunctionInvocationSupported = isFunctionInvocationSupported;
    }
}
