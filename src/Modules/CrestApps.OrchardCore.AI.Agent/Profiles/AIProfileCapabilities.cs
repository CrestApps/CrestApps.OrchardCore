using CrestApps.Core;
using CrestApps.Core.AI.Models;
using CrestApps.Core.AI.Tooling;

namespace CrestApps.OrchardCore.AI.Agent.Profiles;

/// <summary>
/// What a profile can use beyond its instructions: tools, tool instances, agents, MCP and A2A connections, and for an
/// agent whether it is offered to every profile and whether it runs its tools. The configuration guard compares two of
/// these to find what a change adds.
/// </summary>
internal sealed class AIProfileCapabilities
{
    /// <summary>
    /// The key the MCP feature keeps a profile's connections under. The type lives in the MCP package, which this
    /// module does not reference, so the entry is read by its name.
    /// </summary>
    public const string McpMetadataKey = "AIProfileMcpMetadata";

    /// <summary>
    /// The key the A2A feature keeps a profile's connections under, read by name for the same reason.
    /// </summary>
    public const string A2AMetadataKey = "AIProfileA2AMetadata";

    /// <summary>
    /// Gets the names of the registered tools.
    /// </summary>
    public HashSet<string> ToolNames { get; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Gets the names of the tool instances.
    /// </summary>
    public HashSet<string> ToolInstanceNames { get; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Gets the names of the agent profiles the profile may delegate to.
    /// </summary>
    public HashSet<string> AgentNames { get; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Gets the IDs of the MCP connections.
    /// </summary>
    public HashSet<string> McpConnectionIds { get; } = new(StringComparer.Ordinal);

    /// <summary>
    /// Gets the IDs of the A2A connections.
    /// </summary>
    public HashSet<string> A2AConnectionIds { get; } = new(StringComparer.Ordinal);

    /// <summary>
    /// Gets or sets a value indicating whether the profile is an agent offered to every profile on the site.
    /// </summary>
    public bool IsAlwaysAvailableAgent { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the profile is an agent that runs its own tools when another profile
    /// calls it.
    /// </summary>
    public bool AllowsToolInvocation { get; set; }

    /// <summary>
    /// Gets a value indicating whether the profile can run anything at all: a tool, a tool instance, an MCP or A2A
    /// connection.
    /// </summary>
    public bool HasAnyTool
        => ToolNames.Count > 0 || ToolInstanceNames.Count > 0 || McpConnectionIds.Count > 0 || A2AConnectionIds.Count > 0;

    /// <summary>
    /// Reads the capabilities of a profile.
    /// </summary>
    /// <param name="profile">The profile, or <see langword="null"/> for a profile that does not exist yet.</param>
    /// <returns>The capabilities.</returns>
    public static AIProfileCapabilities Read(AIProfile profile)
    {
        var capabilities = new AIProfileCapabilities();

        if (profile is null)
        {
            return capabilities;
        }

        if (profile.TryGet<FunctionInvocationMetadata>(out var tools))
        {
            Add(capabilities.ToolNames, tools.Names);
        }

        if (profile.TryGet<AIToolInstanceMetadata>(out var instances))
        {
            Add(capabilities.ToolInstanceNames, instances.ToolInstanceNames);
        }

        if (profile.TryGet<AgentInvocationMetadata>(out var agents))
        {
            Add(capabilities.AgentNames, agents.Names);
        }

        Add(capabilities.McpConnectionIds, profile.Get<ConnectionsMetadata>(McpMetadataKey)?.ConnectionIds);
        Add(capabilities.A2AConnectionIds, profile.Get<ConnectionsMetadata>(A2AMetadataKey)?.ConnectionIds);

        if (profile.Type == AIProfileType.Agent && profile.TryGet<AgentMetadata>(out var agent))
        {
            capabilities.IsAlwaysAvailableAgent = agent.Availability == AgentAvailability.AlwaysAvailable;
            capabilities.AllowsToolInvocation = agent.AllowToolInvocation;
        }

        return capabilities;
    }

    /// <summary>
    /// Gets what this set has and another does not.
    /// </summary>
    /// <param name="before">The set before the change.</param>
    /// <returns>The added items.</returns>
    public AIProfileCapabilities Except(AIProfileCapabilities before)
    {
        ArgumentNullException.ThrowIfNull(before);

        var added = new AIProfileCapabilities
        {
            IsAlwaysAvailableAgent = IsAlwaysAvailableAgent && !before.IsAlwaysAvailableAgent,
            AllowsToolInvocation = AllowsToolInvocation && !before.AllowsToolInvocation,
        };

        added.ToolNames.UnionWith(ToolNames.Where(name => !before.ToolNames.Contains(name)));
        added.ToolInstanceNames.UnionWith(ToolInstanceNames.Where(name => !before.ToolInstanceNames.Contains(name)));
        added.AgentNames.UnionWith(AgentNames.Where(name => !before.AgentNames.Contains(name)));
        added.McpConnectionIds.UnionWith(McpConnectionIds.Where(id => !before.McpConnectionIds.Contains(id)));
        added.A2AConnectionIds.UnionWith(A2AConnectionIds.Where(id => !before.A2AConnectionIds.Contains(id)));

        return added;
    }

    private static void Add(HashSet<string> set, IEnumerable<string> values)
    {
        if (values is null)
        {
            return;
        }

        set.UnionWith(values.Where(value => !string.IsNullOrWhiteSpace(value)).Select(value => value.Trim()));
    }

    /// <summary>
    /// The shape of the MCP and A2A connection entries of a profile.
    /// </summary>
    private sealed class ConnectionsMetadata
    {
        public string[] ConnectionIds { get; set; }
    }
}
