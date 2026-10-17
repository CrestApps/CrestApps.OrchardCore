namespace CrestApps.OrchardCore.ContactCenter.Reports.Services;

/// <summary>
/// The first entries of an agent activity timeline, and the counts of every entry of it.
/// </summary>
internal sealed class AgentActivitySelection
{
    /// <summary>
    /// Gets the first entries, in the order they started.
    /// </summary>
    public IReadOnlyList<AgentActivityEntry> Entries { get; init; } = [];

    /// <summary>
    /// Gets how many entries the period holds.
    /// </summary>
    public int TotalCount { get; init; }

    /// <summary>
    /// Gets how many of the entries are states.
    /// </summary>
    public int StateCount { get; init; }

    /// <summary>
    /// Gets how many of the entries are offers.
    /// </summary>
    public int OfferCount { get; init; }

    /// <summary>
    /// Gets how many of the entries are call changes.
    /// </summary>
    public int CallCount { get; init; }

    /// <summary>
    /// Gets how many of the entries are flagged.
    /// </summary>
    public int FlaggedCount { get; init; }
}
