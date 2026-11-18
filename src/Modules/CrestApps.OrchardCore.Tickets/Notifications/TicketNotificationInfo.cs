namespace CrestApps.OrchardCore.Tickets.Notifications;

/// <summary>
/// What a ticket notification is about, kept on the notification for its display and its visibility.
/// </summary>
public sealed class TicketNotificationInfo
{
    /// <summary>
    /// Gets or sets the ticket.
    /// </summary>
    public string TicketId { get; set; }

    /// <summary>
    /// Gets or sets the ticket's number, as it was when the notification was sent.
    /// </summary>
    public string Number { get; set; }

    /// <summary>
    /// Gets or sets the ticket's subject, as it was when the notification was sent.
    /// </summary>
    public string Title { get; set; }

    /// <summary>
    /// Gets or sets the ticket's desk, as it was when the notification was sent.
    /// </summary>
    public string DeskId { get; set; }

    /// <summary>
    /// Gets or sets the service-level metric, for a service-level notification.
    /// </summary>
    public string Metric { get; set; }

    /// <summary>
    /// Gets or sets whether the target was missed (or is only close), for a service-level notification.
    /// </summary>
    public bool Breached { get; set; }

    /// <summary>
    /// Gets or sets the ticket merged into this one, for a merge notification.
    /// </summary>
    public string MergedNumber { get; set; }

    /// <summary>
    /// Gets or sets the status the ticket moved to, for a status notification.
    /// </summary>
    public string Status { get; set; }

    /// <summary>
    /// Gets or sets who made the change, by name.
    /// </summary>
    public string By { get; set; }
}
