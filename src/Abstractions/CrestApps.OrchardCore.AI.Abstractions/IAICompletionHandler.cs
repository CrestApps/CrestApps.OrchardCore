using CrestApps.OrchardCore.AI.Models;

namespace CrestApps.OrchardCore.AI;

public interface IAICompletionHandler
{
    Task ReceivedUpdateAsync(ReceivedUpdateContext context);
}
