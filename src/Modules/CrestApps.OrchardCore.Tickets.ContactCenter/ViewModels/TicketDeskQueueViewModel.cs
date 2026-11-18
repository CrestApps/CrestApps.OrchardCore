using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace CrestApps.OrchardCore.Tickets.ContactCenter.ViewModels;

/// <summary>
/// The Contact Center queue of a desk.
/// </summary>
public class TicketDeskQueueViewModel
{
    /// <summary>
    /// Gets or sets the queue, or <see langword="null"/> for none.
    /// </summary>
    public string QueueId { get; set; }

    /// <summary>
    /// Gets or sets the queues to choose from.
    /// </summary>
    [BindNever]
    public List<SelectListItem> Queues { get; set; } = [];
}
