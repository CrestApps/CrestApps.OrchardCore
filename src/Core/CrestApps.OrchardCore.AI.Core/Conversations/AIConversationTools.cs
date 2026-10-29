using CrestApps.Core.AI.Completions;
using CrestApps.Core.AI.Handlers;
using CrestApps.Core.AI.Models;
using CrestApps.Core.AI.Tooling;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.AI.Core.Conversations;

/// <summary>
/// Puts the tools a conversation's profile is configured with in front of a completion that does not go through
/// the orchestrator, together with the system tools the channel adds.
/// </summary>
/// <remarks>
/// Automated SMS and turn-based phone conversations call the completion service directly. The function-invocation
/// handler only offers the tools in the context's scoped entries, which the orchestrator normally fills, so without
/// this the model saw no tools but the channel's own. The profile's tools, tool instances, MCP connections and
/// agents now come from the same registry the orchestrator reads, and the channel's system tools are added after
/// them.
/// </remarks>
public static class AIConversationTools
{
    /// <summary>
    /// Sets the completion's tools: the profile's tools from the registry plus <paramref name="systemTools"/>.
    /// </summary>
    /// <param name="toolRegistry">The tool registry.</param>
    /// <param name="context">The completion context built for the profile.</param>
    /// <param name="systemTools">The channel's system tools, such as ending a call or transferring it.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The entries offered to the model.</returns>
    public static async Task<IReadOnlyList<ToolRegistryEntry>> AttachAsync(
        IToolRegistry toolRegistry,
        AICompletionContext context,
        IEnumerable<ToolRegistryEntry> systemTools,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        var entries = new List<ToolRegistryEntry>();

        if (toolRegistry is not null && !context.DisableTools)
        {
            entries.AddRange(await toolRegistry.GetAllAsync(context, cancellationToken) ?? []);
        }

        foreach (var systemTool in systemTools ?? [])
        {
            if (systemTool is not null && !entries.Exists(entry => string.Equals(entry.Name, systemTool.Name, StringComparison.Ordinal)))
            {
                entries.Add(systemTool);
            }
        }

        context.AdditionalProperties[FunctionInvocationAICompletionServiceHandler.ScopedEntriesKey] = entries;

        return entries;
    }

    /// <summary>
    /// The time limit a tool on a phone call gets when it does not set its own: long enough for an ordinary lookup,
    /// short enough that a caller does not hang up on the silence.
    /// </summary>
    public static readonly TimeSpan DefaultCallToolTimeout = TimeSpan.FromSeconds(8);

    /// <summary>
    /// Puts a time limit on every tool the completion offers but the channel's system tools, which only record a
    /// decision and return at once.
    /// </summary>
    /// <param name="context">The completion context whose tools <see cref="AttachAsync"/> set.</param>
    /// <param name="timeout">The limit for tools that do not set their own.</param>
    /// <param name="started">Called with a tool's name when it starts running.</param>
    /// <param name="finished">Called with a tool's name when it stops running.</param>
    /// <returns>The entries now offered to the model.</returns>
    public static IReadOnlyList<ToolRegistryEntry> LimitRunTime(
        AICompletionContext context,
        TimeSpan timeout,
        Action<string> started = null,
        Action<string> finished = null)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (!context.AdditionalProperties.TryGetValue(FunctionInvocationAICompletionServiceHandler.ScopedEntriesKey, out var value) ||
            value is not IEnumerable<ToolRegistryEntry> current)
        {
            return [];
        }

        // Copies, never the registry's own entries: a registry may hand the same instances to every request.
        var limited = current
            .Where(entry => entry is not null)
            .Select(entry => entry.Source == ToolRegistryEntrySource.System || entry.CreateAsync is null
                ? entry
                : Limit(entry, timeout, started, finished))
            .ToList();

        context.AdditionalProperties[FunctionInvocationAICompletionServiceHandler.ScopedEntriesKey] = limited;

        return limited;
    }

    /// <summary>
    /// Creates a registry entry for a system tool registered as a keyed <see cref="AITool"/>.
    /// </summary>
    /// <param name="toolName">The tool's registered name.</param>
    /// <param name="description">What the tool does.</param>
    /// <returns>The entry.</returns>
    public static ToolRegistryEntry SystemTool(string toolName, string description)
    {
        ArgumentException.ThrowIfNullOrEmpty(toolName);

        return new ToolRegistryEntry
        {
            Id = toolName,
            Name = toolName,
            Description = description,
            Source = ToolRegistryEntrySource.System,
            CreateAsync = serviceProvider => ValueTask.FromResult(serviceProvider.GetKeyedService<AITool>(toolName)),
        };
    }

    private static ToolRegistryEntry Limit(ToolRegistryEntry entry, TimeSpan timeout, Action<string> started, Action<string> finished)
    {
        var create = entry.CreateAsync;

        return new ToolRegistryEntry
        {
            Id = entry.Id,
            Name = entry.Name,
            Description = entry.Description,
            Source = entry.Source,
            SourceId = entry.SourceId,
            CreateAsync = async serviceProvider =>
            {
                var tool = await create(serviceProvider);

                return tool is AIFunction function and not TimeLimitedAIFunction
                    ? new TimeLimitedAIFunction(function, timeout, started, finished)
                    : tool;
            },
        };
    }
}
