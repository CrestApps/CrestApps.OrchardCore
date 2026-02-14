namespace CrestApps.OrchardCore.AI.Mcp.Core.Models;

/// <summary>
/// The result of a pre-intent capability resolution, containing semantically
/// matched MCP capabilities for a given user prompt.
/// </summary>
public sealed class McpCapabilityResolutionResult
{
    /// <summary>
    /// Gets the list of MCP capabilities that matched the user prompt
    /// above the configured similarity threshold, ordered by descending similarity score.
    /// </summary>
    public IReadOnlyList<McpCapabilityCandidate> Candidates { get; }

    /// <summary>
    /// Gets a value indicating whether any relevant capabilities were found.
    /// </summary>
    public bool HasRelevantCapabilities => Candidates.Count > 0;

    /// <summary>
    /// Gets the distinct set of connection IDs that have at least one relevant capability.
    /// </summary>
    public IReadOnlySet<string> RelevantConnectionIds { get; }

    public McpCapabilityResolutionResult(IReadOnlyList<McpCapabilityCandidate> candidates)
    {
        Candidates = candidates ?? [];
        RelevantConnectionIds = new HashSet<string>(
            Candidates.Select(c => c.ConnectionId),
            StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Returns an empty result with no candidates.
    /// </summary>
    public static McpCapabilityResolutionResult Empty { get; } = new([]);
}
