using CrestApps.OrchardCore.Tickets.Models;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace CrestApps.OrchardCore.Tickets.ViewModels;

/// <summary>
/// The editor of a desk.
/// </summary>
public class TicketDeskViewModel
{
    /// <summary>
    /// Gets or sets the name.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets what the desk is for.
    /// </summary>
    public string Description { get; set; }

    /// <summary>
    /// Gets or sets who the desk serves.
    /// </summary>
    public TicketDeskAudience Audience { get; set; }

    /// <summary>
    /// Gets or sets the team new tickets go to.
    /// </summary>
    public string DefaultTeamId { get; set; }

    /// <summary>
    /// Gets or sets the business hours schedule.
    /// </summary>
    public string HoursScheduleId { get; set; }

    /// <summary>
    /// Gets or sets the service-level policy.
    /// </summary>
    public string ServiceLevelPolicyId { get; set; }

    /// <summary>
    /// Gets or sets the ticket types the desk offers.
    /// </summary>
    public IList<string> TicketTypes { get; set; } = [];

    /// <summary>
    /// Gets or sets the reopen window, in days.
    /// </summary>
    public int ReopenWindowDays { get; set; }

    /// <summary>
    /// Gets or sets the auto-close delay, in days.
    /// </summary>
    public int AutoCloseDays { get; set; }

    /// <summary>
    /// Gets or sets how new tickets are given to a team member; empty leaves them for an agent to take.
    /// </summary>
    public string AssignmentStrategy { get; set; }

    /// <summary>
    /// Gets or sets whether requesters are e-mailed.
    /// </summary>
    public bool NotifyRequester { get; set; }

    /// <summary>
    /// Gets or sets whether assignees are told.
    /// </summary>
    public bool NotifyAssignee { get; set; }

    /// <summary>
    /// Gets or sets the policies to choose from.
    /// </summary>
    [Microsoft.AspNetCore.Mvc.ModelBinding.BindNever]
    public IList<SelectListItem> Policies { get; set; } = [];

    /// <summary>
    /// Gets or sets the ticket types to choose from.
    /// </summary>
    [Microsoft.AspNetCore.Mvc.ModelBinding.BindNever]
    public IList<SelectListItem> TicketTypeOptions { get; set; } = [];
}
