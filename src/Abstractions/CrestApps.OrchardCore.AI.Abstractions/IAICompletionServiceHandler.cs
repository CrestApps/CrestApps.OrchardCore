using CrestApps.OrchardCore.AI.Models;

namespace CrestApps.OrchardCore.AI;

public interface IAICompletionServiceHandler
{
    Task ConfigureAsync(CompletionServiceConfigureContext context);
}
