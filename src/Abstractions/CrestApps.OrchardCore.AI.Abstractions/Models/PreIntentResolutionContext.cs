namespace CrestApps.OrchardCore.AI.Models;

/// <summary>
/// Contains the result of a pre-intent capability resolution pass.
/// Provides the intent detector with a filtered set of relevant external capabilities
/// so it can make an informed decision about whether to route to external services.
/// </summary>
public sealed class PreIntentResolutionContext
{
    /// <summary>
    /// Gets the list of capability summaries matched against the user prompt,
    /// ordered by descending similarity score.
    /// </summary>
    public IReadOnlyList<CapabilitySummary> Candidates { get; }

    /// <summary>
    /// Gets a value indicating whether any relevant capabilities were found.
    /// </summary>
    public bool HasRelevantCapabilities => Candidates.Count > 0;

    /// <summary>
    /// Gets the distinct set of source IDs that have at least one relevant capability.
    /// </summary>
    public IReadOnlySet<string> RelevantSourceIds { get; }

    public PreIntentResolutionContext(IReadOnlyList<CapabilitySummary> candidates)
    {
        Candidates = candidates ?? [];
        RelevantSourceIds = new HashSet<string>(
            Candidates.Select(c => c.SourceId),
            StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Returns an empty result with no candidates.
    /// </summary>
    public static PreIntentResolutionContext Empty { get; } = new([]);
}
