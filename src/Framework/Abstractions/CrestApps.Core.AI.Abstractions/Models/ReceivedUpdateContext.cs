using Microsoft.Extensions.AI;

namespace CrestApps.Core.AI.Models;

public sealed class ReceivedUpdateContext
{
    public ReceivedUpdateContext(ChatResponseUpdate update)
    {
        ArgumentNullException.ThrowIfNull(update);

        Update = update;
    }

    public ChatResponseUpdate Update { get; }
}
