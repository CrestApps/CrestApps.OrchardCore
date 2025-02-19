using CrestApps.OrchardCore.AI.Models;

namespace CrestApps.OrchardCore.AI;

public interface IAICompletionHandler
{
    Task ReceivedMessageAsync(ReceivedMessageContext context);

    Task ReceivedUpdateAsync(ReceivedUpdateContext context);
}
