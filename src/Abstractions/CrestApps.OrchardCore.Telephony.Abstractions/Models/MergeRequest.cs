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
    /// Gets or sets the legacy identifier of the primary call that hosts the conference.
    /// </summary>
    public string PrimaryCallId { get; set; }

    /// <summary>
    /// Gets or sets the legacy identifier of the secondary call to merge into the conference.
    /// </summary>
    public string SecondaryCallId { get; set; }

    /// <summary>
    /// Gets or sets an optional name for the resulting conference.
    /// </summary>
    public string ConferenceName { get; set; }

    /// <summary>
    /// Gets the distinct call identifiers supplied by the current or legacy request shape.
    /// </summary>
    /// <returns>The call identifiers to merge.</returns>
    public IReadOnlyList<string> GetCallIds()
    {
        var callIds = CallIds
            .Where(callId => !string.IsNullOrWhiteSpace(callId))
            .Distinct(StringComparer.Ordinal)
            .ToList();

        if (callIds.Count == 0)
        {
            if (!string.IsNullOrWhiteSpace(PrimaryCallId))
            {
                callIds.Add(PrimaryCallId);
            }

            if (!string.IsNullOrWhiteSpace(SecondaryCallId) &&
                !callIds.Contains(SecondaryCallId, StringComparer.Ordinal))
            {
                callIds.Add(SecondaryCallId);
            }
        }

        return callIds;
    }
}
