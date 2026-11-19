namespace CrestApps.OrchardCore.Tickets.TimeTracker;

/// <summary>
/// Time a person logs on a ticket after the fact.
/// </summary>
public sealed class TicketTimeLog
{
    /// <summary>
    /// The most time one entry may log, in minutes.
    /// </summary>
    public const int MaxMinutes = 24 * 60;

    /// <summary>
    /// Gets or sets whose time it is.
    /// </summary>
    public string UserId { get; set; }

    /// <summary>
    /// Gets or sets their name, as their time shows it.
    /// </summary>
    public string UserName { get; set; }

    /// <summary>
    /// Gets or sets who logs it.
    /// </summary>
    public string Actor { get; set; }

    /// <summary>
    /// Gets or sets how long they worked, in minutes.
    /// </summary>
    public int Minutes { get; set; }

    /// <summary>
    /// Gets or sets when they started, in UTC, or <see langword="null"/> when the work just ended.
    /// </summary>
    public DateTime? StartUtc { get; set; }

    /// <summary>
    /// Gets or sets what they did.
    /// </summary>
    public string Note { get; set; }

    /// <summary>
    /// Gets or sets whether the time is billed to the customer; time not billed goes to the desk's not-billed stage,
    /// when it has one.
    /// </summary>
    public bool Billable { get; set; } = true;
}
