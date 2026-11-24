namespace CrestApps.OrchardCore.Tickets.ViewModels;

/// <summary>
/// The approval steps of a ticket type.
/// </summary>
public class TicketApprovalSettingsViewModel
{
    /// <summary>
    /// Gets or sets the steps, in order.
    /// </summary>
    public List<TicketApprovalStepViewModel> Steps { get; set; } = [];

    /// <summary>
    /// Gets or sets the status a ticket waits in.
    /// </summary>
    public string WaitingStatusId { get; set; }

    /// <summary>
    /// Gets or sets the statuses to choose from.
    /// </summary>
    [Microsoft.AspNetCore.Mvc.ModelBinding.BindNever]
    public List<Microsoft.AspNetCore.Mvc.Rendering.SelectListItem> Statuses { get; set; } = [];
}

/// <summary>
/// One approval step of a ticket type.
/// </summary>
public class TicketApprovalStepViewModel
{
    /// <summary>
    /// Gets or sets the step's name.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets the users who may decide it.
    /// </summary>
    public List<string> ApproverIds { get; set; } = [];

    /// <summary>
    /// Gets or sets whether the requester's manager may decide it.
    /// </summary>
    public bool RequesterManager { get; set; }
}
