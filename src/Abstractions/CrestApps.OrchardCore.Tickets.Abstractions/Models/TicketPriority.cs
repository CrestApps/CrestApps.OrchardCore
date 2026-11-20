using CrestApps.Core.Models;

namespace CrestApps.OrchardCore.Tickets.Models;

/// <summary>
/// How urgent a ticket is, such as <em>Urgent</em>. Its name is what service-level targets are set for.
/// </summary>
public sealed class TicketPriority : CatalogItem, ITicketSettingsRecord
{
    /// <summary>
    /// Gets or sets the priority's name.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets its rank; higher is more urgent.
    /// </summary>
    public int Rank { get; set; }

    /// <summary>
    /// Gets or sets the color of its badge, a CSS color.
    /// </summary>
    public string Color { get; set; }

    /// <summary>
    /// Gets or sets when it was created, in UTC.
    /// </summary>
    public DateTime CreatedUtc { get; set; }

    /// <summary>
    /// Gets or sets when it last changed, in UTC.
    /// </summary>
    public DateTime? ModifiedUtc { get; set; }
}
