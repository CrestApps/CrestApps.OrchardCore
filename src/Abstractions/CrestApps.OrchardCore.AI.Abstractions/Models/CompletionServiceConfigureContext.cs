using Microsoft.Extensions.AI;

namespace CrestApps.OrchardCore.AI.Models;

public sealed class CompletionServiceConfigureContext
{
    public ChatOptions ChatOptions { get; }

    public readonly AIProfile Profile;

    public bool IsFunctionInvocationSupported { get; }

    public CompletionServiceConfigureContext(
        ChatOptions chatOptions,
        AIProfile profile,
        bool isFunctionInvocationSupported)
    {
        ArgumentNullException.ThrowIfNull(chatOptions);
        ArgumentNullException.ThrowIfNull(profile);

        ChatOptions = chatOptions;
        Profile = profile;
        IsFunctionInvocationSupported = isFunctionInvocationSupported;
    }
}
