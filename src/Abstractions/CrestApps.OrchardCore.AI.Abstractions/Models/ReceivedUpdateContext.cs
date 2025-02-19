using Microsoft.Extensions.AI;

namespace CrestApps.OrchardCore.AI.Models;

public sealed class ReceivedUpdateContext
{
    public ReceivedUpdateContext(StreamingChatCompletionUpdate update)
    {
        ArgumentNullException.ThrowIfNull(update);

        Update = update;
    }

    public StreamingChatCompletionUpdate Update { get; }
}
