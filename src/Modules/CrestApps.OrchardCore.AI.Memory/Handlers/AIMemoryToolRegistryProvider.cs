using CrestApps.OrchardCore.AI.Models;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CrestApps.OrchardCore.AI.Memory.Handlers;

internal sealed class AIMemoryToolRegistryProvider : IToolRegistryProvider
{
    private readonly AIToolDefinitionOptions _toolDefinitions;
    private readonly ILogger _logger;

    public AIMemoryToolRegistryProvider(
        IOptions<AIToolDefinitionOptions> toolDefinitions,
        ILogger<AIMemoryToolRegistryProvider> logger)
    {
        _toolDefinitions = toolDefinitions.Value;
        _logger = logger;
    }

    public Task<IReadOnlyList<ToolRegistryEntry>> GetToolsAsync(
        AICompletionContext context,
        CancellationToken cancellationToken = default)
    {
        if (context?.AdditionalProperties is null ||
            !context.AdditionalProperties.TryGetValue(MemoryConstants.CompletionContextKeys.HasMemory, out var value) ||
            value is not true)
        {
            if (_logger.IsEnabled(LogLevel.Debug))
            {
                _logger.LogDebug("Memory tool registry skipped because HasMemory was not set on the completion context.");
            }

            return Task.FromResult<IReadOnlyList<ToolRegistryEntry>>([]);
        }

        var entries = new List<ToolRegistryEntry>();

        foreach (var (name, entry) in _toolDefinitions.Tools)
        {
            if (!entry.IsSystemTool || !entry.HasPurpose(AIToolPurposes.Memory))
            {
                continue;
            }

            entries.Add(new ToolRegistryEntry
            {
                Id = name,
                Name = name,
                Description = entry.Description ?? entry.Title ?? name,
                Source = ToolRegistryEntrySource.System,
                CreateAsync = sp => ValueTask.FromResult(sp.GetKeyedService<AITool>(name)),
            });
        }

        if (_logger.IsEnabled(LogLevel.Debug))
        {
            _logger.LogDebug("Memory tool registry returned {Count} tool(s): {Tools}.", entries.Count, string.Join(", ", entries.Select(entry => entry.Name)));
        }

        return Task.FromResult<IReadOnlyList<ToolRegistryEntry>>(entries);
    }
}
