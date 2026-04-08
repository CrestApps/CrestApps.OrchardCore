using CrestApps.Core.AI.Models;
using CrestApps.Core.AI.Tooling;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace CrestApps.Core.AI.Orchestration;

/// <summary>
/// Provides user-selected (non-system) tools from <see cref="AIToolDefinitionOptions"/>
/// to the tool registry. Tools are filtered by the <see cref="AICompletionContext.ToolNames"/>
/// configured on the AI profile.
/// </summary>
internal sealed class ProfileToolRegistryProvider : IToolRegistryProvider
{
    private readonly IOptions<AIToolDefinitionOptions> _toolDefinitions;

    public ProfileToolRegistryProvider(IOptions<AIToolDefinitionOptions> toolDefinitions)
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
            if (!toolDefinitions.TryGetValue(toolName, out var definition))
            {
                continue;
            }

            // Skip system tools — they are provided by SystemToolRegistryProvider.
            if (definition.IsSystemTool)
            {
                continue;
            }

            var name = toolName;

            entries.Add(new ToolRegistryEntry
            {
                Id = name,
                Name = name,
                Description = definition.Description ?? definition.Title ?? name,
                Source = ToolRegistryEntrySource.Local,
                CreateAsync = (sp) => ValueTask.FromResult(sp.GetKeyedService<AITool>(name)),
            });
        }

        return Task.FromResult<IReadOnlyList<ToolRegistryEntry>>(entries);
    }
}
