using CrestApps.Core.AI.Completions;
using CrestApps.Core.AI.Models;
using CrestApps.Core.AI.Tooling;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace CrestApps.Core.AI.Orchestration;

/// <summary>
/// Provides system tools to the tool registry. System tools are automatically
/// included by the orchestrator based on context availability and are not user-selectable.
/// </summary>
/// <remarks>
/// Tools tagged with <see cref="AIToolPurposes.DataSourceSearch"/> are only included
/// when <see cref="AICompletionContext.DataSourceId"/> is set (a data source is attached).
/// Tools tagged with <see cref="AIToolPurposes.DocumentProcessing"/> are only included
/// when the context signals that documents are available via the
/// <see cref="AICompletionContextKeys.HasDocuments"/> additional property.
/// </remarks>
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
        var hasDataSource = !string.IsNullOrEmpty(context?.DataSourceId);
        var hasDocuments = context?.AdditionalProperties is not null
            && context.AdditionalProperties.TryGetValue(AICompletionContextKeys.HasDocuments, out var val)
                && val is true;
        var hasMemory = context?.AdditionalProperties is not null
            && context.AdditionalProperties.TryGetValue(AICompletionContextKeys.HasMemory, out var hasMemoryValue)
                && hasMemoryValue is true;

        var entries = new List<ToolRegistryEntry>();

        foreach (var (name, entry) in _toolOptions.Tools)
        {
            if (!entry.IsSystemTool)
            {
                continue;
            }

            if (entry.HasPurpose(AIToolPurposes.DataSourceSearch) && !hasDataSource)
            {
                continue;
            }

            if (entry.HasPurpose(AIToolPurposes.DocumentProcessing) && !hasDocuments)
            {
                continue;
            }

            if (entry.HasPurpose(AIToolPurposes.Memory) && !hasMemory)
            {
                continue;
            }

            entries.Add(new ToolRegistryEntry
            {
                Id = name,
                Name = name,
                Description = entry.Description ?? entry.Title ?? name,
                Source = ToolRegistryEntrySource.System,
                CreateAsync = (sp) => ValueTask.FromResult(sp.GetKeyedService<AITool>(name)),
            });
        }

        return Task.FromResult<IReadOnlyList<ToolRegistryEntry>>(entries);
    }
}
