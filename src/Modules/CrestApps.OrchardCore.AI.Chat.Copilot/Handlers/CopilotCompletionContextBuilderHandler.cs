using CrestApps.OrchardCore.AI.Chat.Copilot.Models;
using CrestApps.OrchardCore.AI.Models;
using OrchardCore.Entities;

namespace CrestApps.OrchardCore.AI.Chat.Copilot.Handlers;

/// <summary>
/// Reads <see cref="CopilotProfileSettings"/> from the resource (AIProfile or ChatInteraction)
/// and sets <see cref="AICompletionContext.Model"/> so the CopilotOrchestrator can use the
/// correct model without overloading <see cref="AICompletionContext.DeploymentId"/>.
/// </summary>
internal sealed class CopilotCompletionContextBuilderHandler : IAICompletionContextBuilderHandler
{
    public Task BuildingAsync(AICompletionContextBuildingContext context)
    {
        if (context.Resource is not Entity entity)
        {
            return Task.CompletedTask;
        }

        var copilotSettings = entity.As<CopilotProfileSettings>();

        if (copilotSettings is not null && !string.IsNullOrEmpty(copilotSettings.CopilotModel))
        {
            context.Context.Model = copilotSettings.CopilotModel;
        }

        return Task.CompletedTask;
    }

    public Task BuiltAsync(AICompletionContextBuiltContext context)
        => Task.CompletedTask;
}
