using CrestApps.Core.Models;

namespace CrestApps.OrchardCore.Workforce.ContactCenter.Models;

/// <summary>
/// A long-term plan of a planning group's headcount: the work expected week by week against the agents expected,
/// with attrition and hiring classes.
/// </summary>
public sealed class CapacityPlan : CatalogItem
{
    /// <summary>
    /// Gets or sets the name.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets the planning group.
    /// </summary>
    public string PlanningGroupId { get; set; }

    /// <summary>
    /// Gets or sets the first day, a Monday.
    /// </summary>
    public DateOnly StartDate { get; set; }

    /// <summary>
    /// Gets or sets how many weeks it covers, up to 78.
    /// </summary>
    public int Weeks { get; set; } = 26;

    /// <summary>
    /// Gets or sets the handle hours of a week of work at the start, or <see langword="null"/> to take the average of
    /// the planning group's last four weeks of history.
    /// </summary>
    public double? WeeklyWorkloadHours { get; set; }

    /// <summary>
    /// Gets or sets how much the work grows in a year, in percent.
    /// </summary>
    public double GrowthPercentPerYear { get; set; }

    /// <summary>
    /// Gets or sets the paid hours of a full-time agent in a week.
    /// </summary>
    public double HoursPerFte { get; set; } = 37.5;

    /// <summary>
    /// Gets or sets the agents, in full-time equivalents, at the start.
    /// </summary>
    public double CurrentFte { get; set; }

    /// <summary>
    /// Gets or sets the share of agents who leave in a month, in percent.
    /// </summary>
    public double AttritionPercentPerMonth { get; set; } = 2;

    /// <summary>
    /// Gets or sets the hiring classes.
    /// </summary>
    public List<HiringClass> HiringClasses { get; set; } = [];

    /// <summary>
    /// Gets or sets when the plan was made.
    /// </summary>
    public DateTime CreatedUtc { get; set; }

    /// <summary>
    /// Gets or sets when the plan was last changed.
    /// </summary>
    public DateTime? ModifiedUtc { get; set; }
}

/// <summary>
/// New agents who start together, train, then ramp up to full productivity.
/// </summary>
public sealed class HiringClass
{
    /// <summary>
    /// Gets or sets the first day.
    /// </summary>
    public DateOnly StartDate { get; set; }

    /// <summary>
    /// Gets or sets how many agents, in full-time equivalents.
    /// </summary>
    public double Size { get; set; }

    /// <summary>
    /// Gets or sets the weeks of training, when they answer nothing.
    /// </summary>
    public int TrainingWeeks { get; set; } = 4;

    /// <summary>
    /// Gets or sets the weeks after training to reach full productivity, rising evenly.
    /// </summary>
    public int RampWeeks { get; set; } = 4;
}

/// <summary>
/// One week of a capacity plan.
/// </summary>
public sealed class CapacityWeek
{
    /// <summary>
    /// Gets or sets the week's Monday.
    /// </summary>
    public DateOnly WeekStart { get; set; }

    /// <summary>
    /// Gets or sets the handle hours of the week's work.
    /// </summary>
    public double WorkloadHours { get; set; }

    /// <summary>
    /// Gets or sets the agents the work needs, in full-time equivalents.
    /// </summary>
    public double RequiredFte { get; set; }

    /// <summary>
    /// Gets or sets the productive agents expected, in full-time equivalents.
    /// </summary>
    public double AvailableFte { get; set; }

    /// <summary>
    /// Gets or sets the agents in training, in full-time equivalents.
    /// </summary>
    public double InTrainingFte { get; set; }

    /// <summary>
    /// Gets the agents over (positive) or short (negative).
    /// </summary>
    public double Gap => AvailableFte - RequiredFte;
}
