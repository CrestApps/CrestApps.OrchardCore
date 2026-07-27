namespace CrestApps.OrchardCore.Telephony.Models;

/// <summary>
/// Represents a request to merge active calls into a single conference.
/// </summary>
public sealed class MergeRequest
{
    /// <summary>
    /// Gets or sets the identifiers of the calls to merge.
    /// </summary>
    public IReadOnlyList<string> CallIds { get; set; } = [];

    /// <summary>
    /// Gets or sets an optional name for the resulting conference.
    /// </summary>
    public string ConferenceName { get; set; }

    /// <summary>
    /// Gets the distinct, non-empty call identifiers to merge.
    /// </summary>
    /// <returns>The call identifiers to merge.</returns>
    public IReadOnlyList<string> GetCallIds()
    {
        return (CallIds ?? [])
            .Where(callId => !string.IsNullOrWhiteSpace(callId))
            .Distinct(StringComparer.Ordinal)
            .ToList();
    }
}
