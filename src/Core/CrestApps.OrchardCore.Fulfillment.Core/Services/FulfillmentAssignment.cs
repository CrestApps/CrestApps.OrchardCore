namespace CrestApps.OrchardCore.Fulfillment.Core.Services;

/// <summary>
/// The lines assigned to one fulfillment partner, or the line two partners both claim.
/// </summary>
public sealed class FulfillmentAssignment
{
    private FulfillmentAssignment(IReadOnlyDictionary<string, int> lines, string conflictingLine, IReadOnlyList<string> conflictingPartners)
    {
        Lines = lines ?? new Dictionary<string, int>(StringComparer.Ordinal);
        ConflictingLine = conflictingLine;
        ConflictingPartners = conflictingPartners ?? [];
    }

    /// <summary>
    /// Gets the quantity assigned for each line identifier; empty when the partner makes none of the lines.
    /// </summary>
    public IReadOnlyDictionary<string, int> Lines { get; }

    /// <summary>
    /// Gets the title of a line this partner and another both claim, or <see langword="null"/>.
    /// </summary>
    public string ConflictingLine { get; }

    /// <summary>
    /// Gets the names of the partners that claim <see cref="ConflictingLine"/>.
    /// </summary>
    public IReadOnlyList<string> ConflictingPartners { get; }

    /// <summary>
    /// Gets a value indicating whether two partners claim the same line, so a person must decide.
    /// </summary>
    public bool HasConflict
        => ConflictingLine is not null;

    /// <summary>
    /// Creates the assignment of some lines to the partner.
    /// </summary>
    /// <param name="lines">The quantity for each line identifier.</param>
    /// <returns>The assignment.</returns>
    public static FulfillmentAssignment Assigned(IReadOnlyDictionary<string, int> lines)
        => new(lines, null, null);

    /// <summary>
    /// Creates the result for a line two partners both claim.
    /// </summary>
    /// <param name="line">The line's title.</param>
    /// <param name="partners">The partners that claim it.</param>
    /// <returns>The result.</returns>
    public static FulfillmentAssignment Conflict(string line, IReadOnlyList<string> partners)
        => new(null, line, partners);
}
