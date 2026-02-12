using CrestApps.OrchardCore.AI.Models;

namespace CrestApps.OrchardCore.AI.Core.Handlers;

/// <summary>
/// Orchestration context handler that populates document references
/// from a <see cref="ChatInteraction"/> resource.
/// </summary>
internal sealed class DocumentOrchestrationHandler : IOrchestrationContextHandler
{
    public Task BuildingAsync(OrchestrationContextBuildingContext context)
    {
        if (context.Resource is ChatInteraction interaction &&
            interaction.Documents is { Count: > 0 })
        {
            context.Context.Documents = interaction.Documents;
        }

        return Task.CompletedTask;
    }

    public Task BuiltAsync(OrchestrationContextBuiltContext context)
        => Task.CompletedTask;
}
