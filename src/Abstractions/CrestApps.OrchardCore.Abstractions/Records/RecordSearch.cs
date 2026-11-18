using System.Security.Claims;

namespace CrestApps.OrchardCore.Records;

/// <summary>
/// A search for records of one kind, on behalf of a user.
/// </summary>
public sealed class RecordSearch
{
    /// <summary>
    /// Gets or sets the text typed, or empty for the first records.
    /// </summary>
    public string Text { get; set; }

    /// <summary>
    /// Gets or sets the most records to return.
    /// </summary>
    public int Count { get; set; } = 20;

    /// <summary>
    /// Gets or sets the user searching. A source returns only the records this user may see.
    /// </summary>
    public ClaimsPrincipal User { get; set; }
}
