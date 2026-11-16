using CrestApps.OrchardCore.Tickets.Models;

namespace CrestApps.OrchardCore.Tickets.ViewModels;

/// <summary>
/// What the Planning panel posts.
/// </summary>
public class TicketPlanningUpdateViewModel
{
    /// <summary>
    /// Gets or sets the impact.
    /// </summary>
    public TicketLevel? Impact { get; set; }

    /// <summary>
    /// Gets or sets the urgency.
    /// </summary>
    public TicketLevel? Urgency { get; set; }

    /// <summary>
    /// Gets or sets when work is planned to start, in the user's time zone.
    /// </summary>
    public DateTime? EstimatedStart { get; set; }

    /// <summary>
    /// Gets or sets how long the work is expected to take, as people write it: <c>45m</c>, <c>2h</c> or <c>1:30</c>.
    /// </summary>
    public string Estimate { get; set; }

    /// <summary>
    /// Gets or sets how the work is charged.
    /// </summary>
    public TicketBillingType? BillingType { get; set; }
}
