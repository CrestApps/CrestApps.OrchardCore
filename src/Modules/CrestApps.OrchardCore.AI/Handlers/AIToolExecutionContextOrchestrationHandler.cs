using CrestApps.OrchardCore.AI.Models;
using Microsoft.AspNetCore.Http;

namespace CrestApps.OrchardCore.AI.Handlers;

/// <summary>
/// Sets the <see cref="AIToolExecutionContext"/> in <c>HttpContext.Items</c> after
/// the orchestration context is fully built. This removes the need for individual
/// hubs (AIChatHub, ChatInteractionHub) to manually construct and store the context.
/// </summary>
internal sealed class AIToolExecutionContextOrchestrationHandler : IOrchestrationContextBuilderHandler
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public AIToolExecutionContextOrchestrationHandler(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public Task BuildingAsync(OrchestrationContextBuildingContext context)
        => Task.CompletedTask;

    public Task BuiltAsync(OrchestrationContextBuiltContext context)
    {
        var httpContext = _httpContextAccessor.HttpContext;

        httpContext?.Items[nameof(AIToolExecutionContext)] = new AIToolExecutionContext(context.Resource)
        {
            ProviderName = context.Context.SourceName,
            ConnectionName = context.Context.CompletionContext?.ConnectionName,
        };

        return Task.CompletedTask;
    }
}
