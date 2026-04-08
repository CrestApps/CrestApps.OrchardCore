using CrestApps.Core.AI.Completions;
using CrestApps.Core.AI.Models;

namespace CrestApps.Core.AI.Handlers;

public abstract class AICompletionHandlerBase : IAICompletionHandler
{
    public virtual Task ReceivedMessageAsync(ReceivedMessageContext context)
        => Task.CompletedTask;

    public virtual Task ReceivedUpdateAsync(ReceivedUpdateContext context)
        => Task.CompletedTask;
}
