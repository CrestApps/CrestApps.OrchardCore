using Microsoft.Extensions.AI;

namespace CrestApps.OrchardCore.AI.Models;

public sealed class ReceivedMessageContext
{
    public ReceivedMessageContext(ChatCompletion completion)
    {
        ArgumentNullException.ThrowIfNull(completion);

        Completion = completion;
    }

    public ChatCompletion Completion { get; }
}
