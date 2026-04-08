using Microsoft.Extensions.AI;

namespace CrestApps.Core.AI.Models;

public sealed class ReceivedMessageContext
{
    public ReceivedMessageContext(ChatResponse completion)
    {
        ArgumentNullException.ThrowIfNull(completion);

        Completion = completion;
    }

    public ChatResponse Completion { get; }
}
