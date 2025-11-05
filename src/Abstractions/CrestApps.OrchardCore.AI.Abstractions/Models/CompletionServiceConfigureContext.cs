using Microsoft.Extensions.AI;

namespace CrestApps.OrchardCore.AI.Models;

public sealed class CompletionServiceConfigureContext
{
    public ChatOptions ChatOptions { get; }

    public readonly AICompletionContext CompletionContext;

    public bool IsFunctionInvocationSupported { get; }

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
