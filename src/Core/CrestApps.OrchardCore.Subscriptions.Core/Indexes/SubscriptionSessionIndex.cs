using YesSql.Indexes;

namespace CrestApps.OrchardCore.Subscriptions.Core.Indexes;

/// <summary>
/// Indexes subscription sessions for lookup, filtering, and dashboard queries.
/// </summary>
public sealed class SubscriptionSessionIndex : MapIndex
{
    /// <summary>
    /// Gets or sets the unique subscription session identifier.
    /// </summary>
    public string SessionId { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the subscription content item selected for the session.
    /// </summary>
    public string ContentItemId { get; set; }

    /// <summary>
    /// Gets or sets the version identifier of the subscription content item selected for the session.
    /// </summary>
    public string ContentItemVersionId { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the user that owns the session.
    /// </summary>
    public string OwnerId { get; set; }

    /// <summary>
    /// Gets or sets the current status of the subscription session.
    /// </summary>
    public SubscriptionSessionStatus Status { get; set; }

    /// <summary>
    /// Gets or sets the UTC time at which the session was created.
    /// </summary>
    public DateTime CreatedUtc { get; set; }

    /// <summary>
    /// Gets or sets the UTC time at which the session was last modified.
    /// </summary>
    public DateTime ModifiedUtc { get; set; }

    /// <summary>
    /// Gets or sets the UTC time at which the session completed, when applicable.
    /// </summary>
    public DateTime? CompletedUtc { get; set; }
}
