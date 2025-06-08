using Microsoft.Extensions.AI;

namespace CrestApps.OrchardCore.AI.Models;

public sealed class ReceivedMessageContext
{
    public ReceivedMessageContext(ChatResponse completion)
    {
        ArgumentNullException.ThrowIfNull(completion);

        Completion = completion;
    }

    public ChatResponse Completion { get; }
}
