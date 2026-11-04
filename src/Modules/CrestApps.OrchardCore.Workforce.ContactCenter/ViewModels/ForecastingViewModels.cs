using CrestApps.OrchardCore.Workforce.ContactCenter.Models;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;
using OrchardCore.Admin;

namespace CrestApps.OrchardCore.Workforce.ContactCenter.ViewModels;

/// <summary>
/// A list page of the forecasting records.
/// </summary>
public class ForecastingListViewModel
{
    /// <summary>
    /// Gets or sets the planning group filter.
    /// </summary>
    public string PlanningGroupId { get; set; }

    /// <summary>
    /// Gets or sets the planning groups.
    /// </summary>
    [BindNever]
    public IList<SelectListItem> PlanningGroupOptions { get; set; } = [];

    /// <summary>
    /// Gets or sets the list.
    /// </summary>
    [BindNever]
    public AdminListContext List { get; set; }
}

/// <summary>
/// A row of the forecast list.
/// </summary>
public sealed class ForecastRow
{
    /// <summary>
    /// Gets or sets the forecast.
    /// </summary>
    public Forecast Forecast { get; set; }

    /// <summary>
    /// Gets or sets the planning group's name.
    /// </summary>
    public string PlanningGroupName { get; set; }
}

/// <summary>
/// The planning group editor.
/// </summary>
public class PlanningGroupEditViewModel
{
    /// <summary>
    /// Gets or sets the identifier, or <see langword="null"/> for a new group.
    /// </summary>
    public string Id { get; set; }

    /// <summary>
    /// Gets or sets the name.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets the queues.
    /// </summary>
    public List<string> QueueIds { get; set; } = [];

    /// <summary>
    /// Gets or sets the channel, or empty for every channel.
    /// </summary>
    public string Channel { get; set; }

    /// <summary>
    /// Gets or sets the team that staffs the group.
    /// </summary>
    public string TeamId { get; set; }

    /// <summary>
    /// Gets or sets the service level percent, or <see langword="null"/>.
    /// </summary>
    public double? ServiceLevelPercent { get; set; }

    /// <summary>
    /// Gets or sets the service level threshold in seconds.
    /// </summary>
    public int ServiceLevelSeconds { get; set; }

    /// <summary>
    /// Gets or sets the average speed of answer goal in seconds, or <see langword="null"/>.
    /// </summary>
    public int? AverageSpeedOfAnswerSeconds { get; set; }

    /// <summary>
    /// Gets or sets the highest occupancy in percent.
    /// </summary>
    public double MaxOccupancyPercent { get; set; }

    /// <summary>
    /// Gets or sets the concurrency.
    /// </summary>
    public int Concurrency { get; set; }

    /// <summary>
    /// Gets or sets the patience in seconds, or <see langword="null"/>.
    /// </summary>
    public int? PatienceSeconds { get; set; }

    /// <summary>
    /// Gets or sets the shrinkage in percent.
    /// </summary>
    public double ShrinkagePercent { get; set; }

    /// <summary>
    /// Gets or sets the weeks of history.
    /// </summary>
    public int HistoryWeeks { get; set; }

    /// <summary>
    /// Gets or sets the queues to pick from.
    /// </summary>
    [BindNever]
    public IList<SelectListItem> QueueOptions { get; set; } = [];

    /// <summary>
    /// Gets or sets the teams to pick from.
    /// </summary>
    [BindNever]
    public IList<SelectListItem> TeamOptions { get; set; } = [];
}

/// <summary>
/// The forecast event editor.
/// </summary>
public class ForecastEventEditViewModel
{
    /// <summary>
    /// Gets or sets the identifier, or <see langword="null"/> for a new event.
    /// </summary>
    public string Id { get; set; }

    /// <summary>
    /// Gets or sets the name.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets the kind.
    /// </summary>
    public ForecastEventKind Kind { get; set; }

    /// <summary>
    /// Gets or sets the first day.
    /// </summary>
    public DateOnly StartDate { get; set; }

    /// <summary>
    /// Gets or sets the last day.
    /// </summary>
    public DateOnly EndDate { get; set; }

    /// <summary>
    /// Gets or sets the change in volume in percent: -100 for a closure, 50 for half as much again.
    /// </summary>
    public double ChangePercent { get; set; }

    /// <summary>
    /// Gets or sets whether it comes back every year.
    /// </summary>
    public bool RecursYearly { get; set; }

    /// <summary>
    /// Gets or sets the planning groups; none for every group.
    /// </summary>
    public List<string> PlanningGroupIds { get; set; } = [];

    /// <summary>
    /// Gets or sets the planning groups to pick from.
    /// </summary>
    [BindNever]
    public IList<SelectListItem> PlanningGroupOptions { get; set; } = [];
}

/// <summary>
/// The form that makes a forecast.
/// </summary>
public class ForecastCreateViewModel
{
    /// <summary>
    /// Gets or sets the planning group.
    /// </summary>
    public string PlanningGroupId { get; set; }

    /// <summary>
    /// Gets or sets the first day.
    /// </summary>
    public DateOnly FromDate { get; set; }

    /// <summary>
    /// Gets or sets how many weeks to forecast.
    /// </summary>
    public int Weeks { get; set; } = 2;

    /// <summary>
    /// Gets or sets the model.
    /// </summary>
    public ForecastModelKind Model { get; set; }

    /// <summary>
    /// Gets or sets the planning groups to pick from.
    /// </summary>
    [BindNever]
    public IList<SelectListItem> PlanningGroupOptions { get; set; } = [];
}

/// <summary>
/// A forecast's page.
/// </summary>
public class ForecastDetailsViewModel
{
    /// <summary>
    /// Gets or sets the forecast.
    /// </summary>
    [BindNever]
    public Forecast Forecast { get; set; }

    /// <summary>
    /// Gets or sets the planning group.
    /// </summary>
    [BindNever]
    public PlanningGroup Group { get; set; }

    /// <summary>
    /// Gets or sets the day whose intervals are shown.
    /// </summary>
    public DateOnly Day { get; set; }

    /// <summary>
    /// Gets or sets the adjustment's first day.
    /// </summary>
    public DateOnly AdjustFrom { get; set; }

    /// <summary>
    /// Gets or sets the adjustment's last day.
    /// </summary>
    public DateOnly AdjustTo { get; set; }

    /// <summary>
    /// Gets or sets the adjustment in percent.
    /// </summary>
    public double AdjustPercent { get; set; }

    /// <summary>
    /// Gets or sets the adjustment's note.
    /// </summary>
    public string AdjustNote { get; set; }
}

/// <summary>
/// A row of the capacity plan list.
/// </summary>
public sealed class CapacityPlanRow
{
    /// <summary>
    /// Gets or sets the plan.
    /// </summary>
    public CapacityPlan Plan { get; set; }

    /// <summary>
    /// Gets or sets the planning group's name.
    /// </summary>
    public string PlanningGroupName { get; set; }
}

/// <summary>
/// The capacity plan editor.
/// </summary>
public class CapacityPlanEditViewModel
{
    /// <summary>
    /// Gets or sets the identifier, or <see langword="null"/> for a new plan.
    /// </summary>
    public string Id { get; set; }

    /// <summary>
    /// Gets or sets the name.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets the planning group.
    /// </summary>
    public string PlanningGroupId { get; set; }

    /// <summary>
    /// Gets or sets the first week; the plan starts on its Monday.
    /// </summary>
    public DateOnly StartDate { get; set; }

    /// <summary>
    /// Gets or sets how many weeks.
    /// </summary>
    public int Weeks { get; set; } = 26;

    /// <summary>
    /// Gets or sets the handle hours of a week of work at the start, or empty for the last four weeks.
    /// </summary>
    public double? WeeklyWorkloadHours { get; set; }

    /// <summary>
    /// Gets or sets the growth a year, in percent.
    /// </summary>
    public double GrowthPercentPerYear { get; set; }

    /// <summary>
    /// Gets or sets the paid hours of a full-time agent in a week.
    /// </summary>
    public double HoursPerFte { get; set; } = 37.5;

    /// <summary>
    /// Gets or sets the agents at the start, or empty for the team's headcount.
    /// </summary>
    public double? CurrentFte { get; set; }

    /// <summary>
    /// Gets or sets the attrition a month, in percent.
    /// </summary>
    public double AttritionPercentPerMonth { get; set; } = 2;

    /// <summary>
    /// Gets or sets the hiring classes; rows without a first day or a size are left out.
    /// </summary>
    public List<HiringClassEditModel> HiringClasses { get; set; } = [];

    /// <summary>
    /// Gets or sets the planning groups.
    /// </summary>
    [BindNever]
    public IList<SelectListItem> PlanningGroupOptions { get; set; } = [];
}

/// <summary>
/// A hiring class row of the capacity plan editor.
/// </summary>
public class HiringClassEditModel
{
    /// <summary>
    /// Gets or sets the first day.
    /// </summary>
    public DateOnly? StartDate { get; set; }

    /// <summary>
    /// Gets or sets how many agents.
    /// </summary>
    public double? Size { get; set; }

    /// <summary>
    /// Gets or sets the weeks of training.
    /// </summary>
    public int TrainingWeeks { get; set; } = 4;

    /// <summary>
    /// Gets or sets the weeks to reach full productivity.
    /// </summary>
    public int RampWeeks { get; set; } = 4;
}
