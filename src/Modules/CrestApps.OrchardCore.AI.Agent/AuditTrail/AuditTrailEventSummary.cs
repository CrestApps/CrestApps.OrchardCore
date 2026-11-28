namespace CrestApps.OrchardCore.AI.Agent.AuditTrail;

/// <summary>
/// The fields the audit trail tools return for every event.
/// </summary>
internal sealed class AuditTrailEventSummary
{
    /// <summary>
    /// Gets or sets the ID of the event.
    /// </summary>
    public string EventId { get; set; }

    /// <summary>
    /// Gets or sets the technical name of the category of the event.
    /// </summary>
    public string Category { get; set; }

    /// <summary>
    /// Gets or sets the technical name of the event.
    /// </summary>
    public string Event { get; set; }

    /// <summary>
    /// Gets or sets the name the Audit Trail admin page shows for the event.
    /// </summary>
    public string Title { get; set; }

    /// <summary>
    /// Gets or sets the ID that ties the event to a record, for example the ID of a content item or of a user.
    /// </summary>
    public string CorrelationId { get; set; }

    /// <summary>
    /// Gets or sets the ID of the user who caused the event.
    /// </summary>
    public string UserId { get; set; }

    /// <summary>
    /// Gets or sets the user name of the user who caused the event.
    /// </summary>
    public string UserName { get; set; }

    /// <summary>
    /// Gets or sets when the event happened, in the time zone of the user or the site.
    /// </summary>
    public string CreatedAt { get; set; }
}
