using CrestApps.AI.Completions;
using CrestApps.AI.Models;

namespace CrestApps.AI.Handlers;

public abstract class AICompletionHandlerBase : IAICompletionHandler
{
    public virtual Task ReceivedMessageAsync(ReceivedMessageContext context)
        => Task.CompletedTask;

    public virtual Task ReceivedUpdateAsync(ReceivedUpdateContext context)
        => Task.CompletedTask;
}
