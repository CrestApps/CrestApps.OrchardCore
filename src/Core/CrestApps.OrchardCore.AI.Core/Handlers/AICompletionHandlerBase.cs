using CrestApps.OrchardCore.AI.Models;

namespace CrestApps.OrchardCore.AI.Core.Handlers;

public abstract class AICompletionHandlerBase : IAICompletionHandler
{
    public virtual Task ReceivedMessageAsync(ReceivedMessageContext context)
        => Task.CompletedTask;

    public virtual Task ReceivedUpdateAsync(ReceivedUpdateContext context)
        => Task.CompletedTask;
}
