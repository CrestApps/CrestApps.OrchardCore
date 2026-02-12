using CrestApps.OrchardCore.AI.Models;
using Microsoft.Extensions.Options;

namespace CrestApps.OrchardCore.AI.Core.Orchestration;

/// <summary>
/// Provides local tool metadata from <see cref="AIToolDefinitionOptions"/> to the tool registry.
/// </summary>
internal sealed class LocalToolRegistryProvider : IToolRegistryProvider
{
    private readonly IOptions<AIToolDefinitionOptions> _toolDefinitions;

    public LocalToolRegistryProvider(IOptions<AIToolDefinitionOptions> toolDefinitions)
    {
        _toolDefinitions = toolDefinitions;
    }

    public Task<IReadOnlyList<ToolRegistryEntry>> GetToolsAsync(
        AICompletionContext context,
        CancellationToken cancellationToken = default)
    {
        var configuredToolNames = context?.ToolNames;

        if (configuredToolNames is null || configuredToolNames.Length == 0)
        {
            return Task.FromResult<IReadOnlyList<ToolRegistryEntry>>([]);
        }

        var toolDefinitions = _toolDefinitions.Value.Tools;
        var entries = new List<ToolRegistryEntry>();

        foreach (var toolName in configuredToolNames)
        {
            if (toolDefinitions.TryGetValue(toolName, out var definition))
            {
                entries.Add(new ToolRegistryEntry
                {
                    Name = toolName,
                    Description = definition.Description ?? definition.Title ?? toolName,
                    Source = ToolRegistryEntrySource.Local,
                });
            }
        }

        return Task.FromResult<IReadOnlyList<ToolRegistryEntry>>(entries);
    }
}
