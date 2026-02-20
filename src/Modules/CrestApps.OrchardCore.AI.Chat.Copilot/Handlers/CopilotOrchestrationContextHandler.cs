using CrestApps.OrchardCore.AI.Chat.Copilot.Models;
using CrestApps.OrchardCore.AI.Models;
using OrchardCore.Entities;

namespace CrestApps.OrchardCore.AI.Chat.Copilot.Handlers;

/// <summary>
/// Reads <see cref="CopilotSessionMetadata"/> from the resource (AIProfile or ChatInteraction)
/// and sets it on <see cref="OrchestrationContext.Properties"/> so the CopilotOrchestrator
/// can read the model name and flags without coupling through <see cref="AICompletionContext"/>.
/// </summary>
internal sealed class CopilotOrchestrationContextHandler : IOrchestrationContextHandler
{
    public Task BuildingAsync(OrchestrationContextBuildingContext context)
    {
        if (context.Resource is not Entity entity)
        {
            return Task.CompletedTask;
        }

        var metadata = entity.As<CopilotSessionMetadata>();

        if (metadata is not null)
        {
            context.Context.Properties[nameof(CopilotSessionMetadata)] = metadata;
        }

        return Task.CompletedTask;
    }

    public Task BuiltAsync(OrchestrationContextBuiltContext context)
        => Task.CompletedTask;
}
