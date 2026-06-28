namespace CrestApps.OrchardCore.Telephony.Models;

/// <summary>
/// Represents a request to merge two active calls into a single conference.
/// </summary>
public sealed class MergeRequest
{
    /// <summary>
    /// Gets or sets the identifier of the primary call that hosts the conference.
    /// </summary>
    public string PrimaryCallId { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the secondary call to merge into the conference.
    /// </summary>
    public string SecondaryCallId { get; set; }

    /// <summary>
    /// Gets or sets an optional name for the resulting conference.
    /// </summary>
    public string ConferenceName { get; set; }
}
