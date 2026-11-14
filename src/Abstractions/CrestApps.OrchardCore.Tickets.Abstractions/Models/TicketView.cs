using CrestApps.Core.Models;

namespace CrestApps.OrchardCore.Tickets.Models;

/// <summary>
/// A saved list of tickets, such as <em>Unassigned</em> or <em>Breaching soon</em>.
/// </summary>
public sealed class TicketView : CatalogItem, ITicketSettingsRecord
{
    /// <summary>
    /// Gets or sets the view's name.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets which tickets it lists, and in what order.
    /// </summary>
    public TicketQuery Query { get; set; } = new();

    /// <summary>
    /// Gets or sets the user who made it; empty for a view everybody shares.
    /// </summary>
    public string OwnerId { get; set; }

    /// <summary>
    /// Gets or sets the team it is shared with; empty shares it with everybody (or nobody, when it has an owner).
    /// </summary>
    public string SharedWithTeamId { get; set; }

    /// <summary>
    /// Gets or sets where it is listed; lower comes first.
    /// </summary>
    public int Order { get; set; }

    /// <summary>
    /// Gets or sets when it was created, in UTC.
    /// </summary>
    public DateTime CreatedUtc { get; set; }

    /// <summary>
    /// Gets or sets when it last changed, in UTC.
    /// </summary>
    public DateTime? ModifiedUtc { get; set; }
}
