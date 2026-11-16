using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace CrestApps.OrchardCore.Tickets.ViewModels;

/// <summary>
/// The Time Tracker stages a desk's ticket work is recorded in.
/// </summary>
public class TicketDeskTimeViewModel
{
    /// <summary>
    /// Gets or sets the stage, or <see langword="null"/> when the desk's tickets record no time.
    /// </summary>
    public string StageId { get; set; }

    /// <summary>
    /// Gets or sets the stage for work not billed to the customer, or <see langword="null"/> when all ticket time goes
    /// to <see cref="StageId"/>.
    /// </summary>
    public string NotBilledStageId { get; set; }

    /// <summary>
    /// Gets or sets the stages to choose from.
    /// </summary>
    [BindNever]
    public List<SelectListItem> Stages { get; set; } = [];
}
