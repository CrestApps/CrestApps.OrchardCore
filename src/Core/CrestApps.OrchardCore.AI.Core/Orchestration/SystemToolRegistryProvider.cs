using CrestApps.OrchardCore.AI.Models;
using Microsoft.Extensions.Options;

namespace CrestApps.OrchardCore.AI.Core.Orchestration;

/// <summary>
/// Provides system tools to the tool registry. System tools are automatically
/// included by the orchestrator based on availability and are not user-selectable.
/// </summary>
internal sealed class SystemToolRegistryProvider : IToolRegistryProvider
{
    private readonly AIToolDefinitionOptions _toolOptions;

    public SystemToolRegistryProvider(IOptions<AIToolDefinitionOptions> toolOptions)
    {
        _toolOptions = toolOptions.Value;
    }

    public Task<IReadOnlyList<ToolRegistryEntry>> GetToolsAsync(
        AICompletionContext context,
        CancellationToken cancellationToken = default)
    {
        var entries = new List<ToolRegistryEntry>();

        foreach (var (name, entry) in _toolOptions.Tools)
        {
            if (!entry.IsSystemTool)
            {
                continue;
            }

            entries.Add(new ToolRegistryEntry
            {
                Name = name,
                Description = entry.Description ?? entry.Title ?? name,
                Source = ToolRegistryEntrySource.System,
            });
        }

        return Task.FromResult<IReadOnlyList<ToolRegistryEntry>>(entries);
    }
}
