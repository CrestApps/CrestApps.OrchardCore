using CrestApps.OrchardCore.AI.Core.Orchestration;
using CrestApps.OrchardCore.AI.Models;
using CrestApps.OrchardCore.AI.Playwright.Models;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.AI.Playwright.Services;

internal sealed class PlaywrightToolRegistryProvider : IToolRegistryProvider
{
    public Task<IReadOnlyList<ToolRegistryEntry>> GetToolsAsync(
        AICompletionContext context,
        CancellationToken cancellationToken = default)
    {
        if (context?.DisableTools == true ||
            context?.AdditionalProperties is null ||
            !context.AdditionalProperties.TryGetValue(PlaywrightConstants.CompletionContextKeys.SessionMetadata, out var metadataObject) ||
            metadataObject is not PlaywrightSessionMetadata { Enabled: true })
        {
            return Task.FromResult<IReadOnlyList<ToolRegistryEntry>>([]);
        }

        var entries = PlaywrightConstants.ToolSets.Deterministic
            .Select(CreateEntry)
            .ToList();

        return Task.FromResult<IReadOnlyList<ToolRegistryEntry>>(entries);
    }

    private static ToolRegistryEntry CreateEntry(string toolName)
    {
        return new ToolRegistryEntry
        {
            Id = $"playwright:{toolName}",
            Name = toolName,
            Description = PlaywrightConstants.GetToolDescription(toolName),
            Source = ToolRegistryEntrySource.System,
            CreateAsync = sp => ValueTask.FromResult(sp.GetKeyedService<AITool>(toolName)),
        };
    }
}
