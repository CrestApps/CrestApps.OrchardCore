namespace CrestApps.Core.ContactCenter;

/// <summary>
/// Normalizes the queue and campaign identifiers a caller selected.
/// </summary>
/// <remarks>
/// The same list arrives from an admin form, from a controller and over the hub, and each of those can
/// carry blanks the operator never typed, stray whitespace, or the same identifier twice in different
/// casing. Normalizing in one place is what stops an agent being counted twice in a queue because two
/// entry points disagreed about whether "Q1" and "q1" are the same queue.
/// </remarks>
public static class AgentMembershipIds
{
    /// <summary>
    /// Drops blanks, trims what is left, and removes case-insensitive duplicates.
    /// </summary>
    /// <param name="values">The selected identifiers, which may be <see langword="null"/>.</param>
    /// <returns>The normalized identifiers; empty when nothing was selected.</returns>
    public static IList<string> Normalize(IEnumerable<string> values)
    {
        return values is null
            ? []
            : values
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Select(value => value.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
    }
}
