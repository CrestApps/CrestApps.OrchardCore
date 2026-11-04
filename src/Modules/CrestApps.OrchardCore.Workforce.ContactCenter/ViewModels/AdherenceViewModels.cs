using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.Workforce.ContactCenter.Models;
using CrestApps.OrchardCore.Workforce.ContactCenter.Services;
using CrestApps.OrchardCore.Workforce.Models;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;
using OrchardCore.Admin;

namespace CrestApps.OrchardCore.Workforce.ContactCenter.ViewModels;

/// <summary>
/// The real-time adherence board.
/// </summary>
public class AdherenceBoardViewModel
{
    /// <summary>
    /// Gets or sets the team shown, or <see langword="null"/> for every team the viewer manages.
    /// </summary>
    public string TeamId { get; set; }

    /// <summary>
    /// Gets or sets the teams the viewer can pick.
    /// </summary>
    [BindNever]
    public IList<SelectListItem> TeamOptions { get; set; } = [];

    /// <summary>
    /// Gets or sets the rows.
    /// </summary>
    [BindNever]
    public IReadOnlyList<AdherenceBoardItem> Rows { get; set; } = [];

    /// <summary>
    /// Gets or sets whether the viewer can add exceptions.
    /// </summary>
    [BindNever]
    public bool CanManage { get; set; }
}

/// <summary>
/// A row of the board with its times in the worker's time zone.
/// </summary>
public sealed class AdherenceBoardItem
{
    /// <summary>
    /// Gets or sets the row.
    /// </summary>
    public AdherenceBoardRow Row { get; set; }

    /// <summary>
    /// Gets or sets the worker's time zone.
    /// </summary>
    public string TimeZoneId { get; set; }

    /// <summary>
    /// Gets or sets the minutes out of adherence.
    /// </summary>
    public int OutMinutes { get; set; }

    /// <summary>
    /// Gets or sets the minutes in the current state.
    /// </summary>
    public int StateMinutes { get; set; }
}

/// <summary>
/// A worker's adherence over one day.
/// </summary>
public class AdherenceDayViewModel
{
    /// <summary>
    /// Gets or sets the worker.
    /// </summary>
    [BindNever]
    public WorkerProfile Worker { get; set; }

    /// <summary>
    /// Gets or sets the day.
    /// </summary>
    public DateOnly Date { get; set; }

    /// <summary>
    /// Gets or sets the computed day.
    /// </summary>
    [BindNever]
    public AdherenceDay Day { get; set; }

    /// <summary>
    /// Gets or sets whether the day shown is the saved one.
    /// </summary>
    [BindNever]
    public bool Saved { get; set; }

    /// <summary>
    /// Gets or sets whether the viewer can add exceptions and save the day.
    /// </summary>
    [BindNever]
    public bool CanManage { get; set; }
}

/// <summary>
/// A row of the exception list.
/// </summary>
public sealed class AdherenceExceptionRow
{
    /// <summary>
    /// Gets or sets the exception.
    /// </summary>
    public AdherenceException Exception { get; set; }

    /// <summary>
    /// Gets or sets the worker's name.
    /// </summary>
    public string WorkerName { get; set; }

    /// <summary>
    /// Gets or sets the team's name.
    /// </summary>
    public string TeamName { get; set; }

    /// <summary>
    /// Gets or sets the time zone the times are shown in.
    /// </summary>
    public string TimeZoneId { get; set; }

    /// <summary>
    /// Gets or sets whether the viewer can remove it.
    /// </summary>
    public bool CanManage { get; set; }
}

/// <summary>
/// The exception list.
/// </summary>
public class AdherenceExceptionListViewModel
{
    /// <summary>
    /// Gets or sets the team filter.
    /// </summary>
    public string TeamId { get; set; }

    /// <summary>
    /// Gets or sets the teams the viewer can pick.
    /// </summary>
    [BindNever]
    public IList<SelectListItem> TeamOptions { get; set; } = [];

    /// <summary>
    /// Gets or sets the list.
    /// </summary>
    [BindNever]
    public AdminListContext List { get; set; }

    /// <summary>
    /// Gets or sets whether the viewer can add exceptions.
    /// </summary>
    [BindNever]
    public bool CanManage { get; set; }
}

/// <summary>
/// The form that records an exception.
/// </summary>
public class AdherenceExceptionEditViewModel
{
    /// <summary>
    /// Gets or sets the worker.
    /// </summary>
    public string WorkerId { get; set; }

    /// <summary>
    /// Gets or sets the day, in the worker's time zone.
    /// </summary>
    public DateOnly Date { get; set; }

    /// <summary>
    /// Gets or sets the start.
    /// </summary>
    public TimeOnly Start { get; set; }

    /// <summary>
    /// Gets or sets the end.
    /// </summary>
    public TimeOnly End { get; set; }

    /// <summary>
    /// Gets or sets the reason.
    /// </summary>
    public AdherenceExceptionReason Reason { get; set; }

    /// <summary>
    /// Gets or sets the note.
    /// </summary>
    public string Note { get; set; }

    /// <summary>
    /// Gets or sets where to go back to.
    /// </summary>
    public string ReturnUrl { get; set; }

    /// <summary>
    /// Gets or sets the workers the viewer can pick.
    /// </summary>
    [BindNever]
    public IList<SelectListItem> WorkerOptions { get; set; } = [];
}

/// <summary>
/// The rule set editor.
/// </summary>
public class AdherenceRuleSetEditViewModel
{
    /// <summary>
    /// Gets or sets the identifier, or <see langword="null"/> for a new set.
    /// </summary>
    public string Id { get; set; }

    /// <summary>
    /// Gets or sets the name.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets the teams.
    /// </summary>
    public List<string> TeamIds { get; set; } = [];

    /// <summary>
    /// Gets or sets the rules, one per kind of activity.
    /// </summary>
    public List<AdherenceRuleEditViewModel> Rules { get; set; } = [];

    /// <summary>
    /// Gets or sets the teams the viewer can pick.
    /// </summary>
    [BindNever]
    public IList<SelectListItem> TeamOptions { get; set; } = [];
}

/// <summary>
/// One rule of the rule set editor.
/// </summary>
public class AdherenceRuleEditViewModel
{
    /// <summary>
    /// Gets or sets the kind of activity.
    /// </summary>
    public ActivityKind Kind { get; set; }

    /// <summary>
    /// Gets or sets the agent states in adherence.
    /// </summary>
    public List<string> AllowedStatuses { get; set; } = [];

    /// <summary>
    /// Gets or sets the reasons, comma-separated, or empty for any.
    /// </summary>
    public string AllowedReasons { get; set; }

    /// <summary>
    /// Gets or sets the start grace in minutes.
    /// </summary>
    public int StartGraceMinutes { get; set; }

    /// <summary>
    /// Gets or sets the end grace in minutes.
    /// </summary>
    public int EndGraceMinutes { get; set; }

    /// <summary>
    /// Gets or sets the shortest slip that counts, in minutes.
    /// </summary>
    public int MinimumOutMinutes { get; set; }

    /// <summary>
    /// Gets or sets the minutes out before an alert.
    /// </summary>
    public int AlertAfterMinutes { get; set; }
}

/// <summary>
/// A row of the agents list: an agent and its worker.
/// </summary>
public sealed class AgentWorkerRow
{
    /// <summary>
    /// Gets or sets the agent.
    /// </summary>
    public AgentProfile Agent { get; set; }

    /// <summary>
    /// Gets or sets the worker, or <see langword="null"/>.
    /// </summary>
    public WorkerProfile Worker { get; set; }
}

/// <summary>
/// The agents list.
/// </summary>
public class AgentWorkerListViewModel
{
    /// <summary>
    /// Gets or sets the team new workers join.
    /// </summary>
    public string TeamId { get; set; }

    /// <summary>
    /// Gets or sets the teams.
    /// </summary>
    [BindNever]
    public IList<SelectListItem> TeamOptions { get; set; } = [];

    /// <summary>
    /// Gets or sets the list.
    /// </summary>
    [BindNever]
    public AdminListContext List { get; set; }

    /// <summary>
    /// Gets or sets how many agents have no worker.
    /// </summary>
    [BindNever]
    public int Missing { get; set; }
}
