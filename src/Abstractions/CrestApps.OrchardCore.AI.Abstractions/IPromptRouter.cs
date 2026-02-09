using CrestApps.OrchardCore.AI.Models;

namespace CrestApps.OrchardCore.AI;

public interface IPromptRouter
{
    Task<IntentProcessingResult> RouteAsync(PromptRoutingContext context, CancellationToken cancellationToken = default);
}
