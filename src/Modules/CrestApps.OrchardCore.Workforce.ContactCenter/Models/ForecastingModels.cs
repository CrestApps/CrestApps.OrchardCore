using CrestApps.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Models;

namespace CrestApps.OrchardCore.Workforce.ContactCenter.Models;

/// <summary>
/// Queues forecast and staffed together: their work, the goal they are answered to, and the team that staffs them.
/// </summary>
public sealed class PlanningGroup : CatalogItem
{
    /// <summary>
    /// Gets or sets the name.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets the queues.
    /// </summary>
    public List<string> QueueIds { get; set; } = [];

    /// <summary>
    /// Gets or sets the channel counted, or <see langword="null"/> for every channel.
    /// </summary>
    public InteractionChannel? Channel { get; set; }

    /// <summary>
    /// Gets or sets the team that staffs the group; a forecast's staffing becomes this team's coverage.
    /// </summary>
    public string TeamId { get; set; }

    /// <summary>
    /// Gets or sets the share to answer within <see cref="ServiceLevelSeconds"/>, in percent; <see langword="null"/>
    /// to staff to the average speed of answer only.
    /// </summary>
    public double? ServiceLevelPercent { get; set; } = 80;

    /// <summary>
    /// Gets or sets the service level's threshold in seconds.
    /// </summary>
    public int ServiceLevelSeconds { get; set; } = 20;

    /// <summary>
    /// Gets or sets the average speed of answer to stay within, in seconds, or <see langword="null"/>.
    /// </summary>
    public int? AverageSpeedOfAnswerSeconds { get; set; }

    /// <summary>
    /// Gets or sets the highest occupancy, in percent.
    /// </summary>
    public double MaxOccupancyPercent { get; set; } = 85;

    /// <summary>
    /// Gets or sets how many pieces of work an agent handles at once.
    /// </summary>
    public int Concurrency { get; set; } = 1;

    /// <summary>
    /// Gets or sets how long customers wait before abandoning, in seconds, for Erlang A; <see langword="null"/> for
    /// Erlang C.
    /// </summary>
    public int? PatienceSeconds { get; set; }

    /// <summary>
    /// Gets or sets the share of paid time agents aren't available to answer: breaks, meetings, absence, in percent.
    /// </summary>
    public double ShrinkagePercent { get; set; } = 30;

    /// <summary>
    /// Gets or sets how many weeks of history a forecast reads.
    /// </summary>
    public int HistoryWeeks { get; set; } = 8;

    /// <summary>
    /// Gets or sets when the group was created.
    /// </summary>
    public DateTime CreatedUtc { get; set; }

    /// <summary>
    /// Gets or sets when the group was last changed.
    /// </summary>
    public DateTime? ModifiedUtc { get; set; }
}

/// <summary>
/// The kind of a forecast event.
/// </summary>
public enum ForecastEventKind
{
    /// <summary>
    /// A holiday.
    /// </summary>
    Holiday,

    /// <summary>
    /// The contact center is closed.
    /// </summary>
    Closure,

    /// <summary>
    /// A promotion or campaign that brings more work.
    /// </summary>
    Promotion,

    /// <summary>
    /// An outage that brings more work.
    /// </summary>
    Outage,

    /// <summary>
    /// Anything else.
    /// </summary>
    Custom,
}

/// <summary>
/// Days whose work differs from the usual: a holiday, a closure, a promotion.
/// </summary>
public sealed class ForecastEvent : CatalogItem
{
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
    /// Gets or sets what the usual volume is multiplied by: zero for a closure, 1.5 for half as much again.
    /// </summary>
    public double VolumeMultiplier { get; set; } = 1;

    /// <summary>
    /// Gets or sets whether the event comes back on the same dates every year.
    /// </summary>
    public bool RecursYearly { get; set; }

    /// <summary>
    /// Gets or sets the planning groups it affects; none for every group.
    /// </summary>
    public List<string> PlanningGroupIds { get; set; } = [];

    /// <summary>
    /// Gets whether the event covers a day.
    /// </summary>
    /// <param name="date">The day.</param>
    /// <returns><see langword="true"/> when it does.</returns>
    public bool Covers(DateOnly date)
    {
        if (!RecursYearly)
        {
            return date >= StartDate && date <= EndDate;
        }

        var start = (StartDate.Month * 100) + StartDate.Day;
        var end = (EndDate.Month * 100) + EndDate.Day;
        var day = (date.Month * 100) + date.Day;

        return start <= end ? day >= start && day <= end : day >= start || day <= end;
    }
}

/// <summary>
/// How a forecast's daily volume is worked out.
/// </summary>
public enum ForecastModelKind
{
    /// <summary>
    /// The model with the lowest error on the last weeks of history.
    /// </summary>
    Auto,

    /// <summary>
    /// The same weekday of the last weeks, the latest weighing most.
    /// </summary>
    WeightedHistory,

    /// <summary>
    /// Holt-Winters triple exponential smoothing with a weekly season.
    /// </summary>
    HoltWinters,
}

/// <summary>
/// Where a forecast is in its life.
/// </summary>
public enum ForecastStatus
{
    /// <summary>
    /// Being reviewed.
    /// </summary>
    Draft,

    /// <summary>
    /// The group's forecast in use; its staffing is the team's coverage.
    /// </summary>
    Active,

    /// <summary>
    /// Replaced by a newer active forecast.
    /// </summary>
    Superseded,
}

/// <summary>
/// A forecast of a planning group's work and the agents it needs, per 15-minute interval.
/// </summary>
public sealed class Forecast : CatalogItem
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
    /// Gets or sets the last day.
    /// </summary>
    public DateOnly ToDate { get; set; }

    /// <summary>
    /// Gets or sets the time zone the days are in.
    /// </summary>
    public string TimeZoneId { get; set; }

    /// <summary>
    /// Gets or sets the model used.
    /// </summary>
    public ForecastModelKind Model { get; set; }

    /// <summary>
    /// Gets or sets the status.
    /// </summary>
    public ForecastStatus Status { get; set; }

    /// <summary>
    /// Gets or sets the intervals with work, in order.
    /// </summary>
    public List<ForecastInterval> Intervals { get; set; } = [];

    /// <summary>
    /// Gets or sets the adjustments made by hand.
    /// </summary>
    public List<ForecastAdjustment> Adjustments { get; set; } = [];

    /// <summary>
    /// Gets or sets each model's error on the last weeks of history.
    /// </summary>
    public List<ForecastBacktest> Backtests { get; set; } = [];

    /// <summary>
    /// Gets or sets the shrinkage used, in percent.
    /// </summary>
    public double ShrinkagePercent { get; set; }

    /// <summary>
    /// Gets or sets who made it.
    /// </summary>
    public string CreatedBy { get; set; }

    /// <summary>
    /// Gets or sets when it was made.
    /// </summary>
    public DateTime CreatedUtc { get; set; }

    /// <summary>
    /// Gets or sets when it became the active forecast.
    /// </summary>
    public DateTime? ActivatedUtc { get; set; }
}

/// <summary>
/// One 15-minute interval of a forecast.
/// </summary>
public sealed class ForecastInterval
{
    /// <summary>
    /// Gets or sets the day, in the forecast's time zone.
    /// </summary>
    public DateOnly Date { get; set; }

    /// <summary>
    /// Gets or sets the interval of the day: 0 for 00:00, 4 for 01:00.
    /// </summary>
    public int Slot { get; set; }

    /// <summary>
    /// Gets or sets the UTC start.
    /// </summary>
    public DateTime StartUtc { get; set; }

    /// <summary>
    /// Gets or sets the work forecast, after adjustments.
    /// </summary>
    public double Volume { get; set; }

    /// <summary>
    /// Gets or sets the average handle time in seconds.
    /// </summary>
    public double AverageHandleSeconds { get; set; }

    /// <summary>
    /// Gets or sets the agents needed answering.
    /// </summary>
    public int RequiredAgents { get; set; }

    /// <summary>
    /// Gets or sets the agents to schedule, with shrinkage.
    /// </summary>
    public int RequiredScheduled { get; set; }

    /// <summary>
    /// Gets or sets the expected service level, as a fraction.
    /// </summary>
    public double ServiceLevel { get; set; }

    /// <summary>
    /// Gets or sets the expected average speed of answer in seconds.
    /// </summary>
    public double AverageSpeedOfAnswerSeconds { get; set; }

    /// <summary>
    /// Gets or sets the expected occupancy, as a fraction.
    /// </summary>
    public double Occupancy { get; set; }
}

/// <summary>
/// A change made by hand to a forecast's volume over some days.
/// </summary>
public sealed class ForecastAdjustment
{
    /// <summary>
    /// Gets or sets the first day.
    /// </summary>
    public DateOnly FromDate { get; set; }

    /// <summary>
    /// Gets or sets the last day.
    /// </summary>
    public DateOnly ToDate { get; set; }

    /// <summary>
    /// Gets or sets the change in percent: 10 for a tenth more.
    /// </summary>
    public double Percent { get; set; }

    /// <summary>
    /// Gets or sets why.
    /// </summary>
    public string Note { get; set; }

    /// <summary>
    /// Gets or sets who made it.
    /// </summary>
    public string By { get; set; }
}

/// <summary>
/// A model's error on the held-out weeks of history.
/// </summary>
public sealed class ForecastBacktest
{
    /// <summary>
    /// Gets or sets the model.
    /// </summary>
    public ForecastModelKind Model { get; set; }

    /// <summary>
    /// Gets or sets the mean absolute percentage error of the daily totals.
    /// </summary>
    public double Mape { get; set; }

    /// <summary>
    /// Gets or sets the weighted absolute percentage error of the daily totals.
    /// </summary>
    public double Wape { get; set; }
}
