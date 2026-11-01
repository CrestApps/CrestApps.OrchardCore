using CrestApps.OrchardCore.Workforce.ContactCenter.Models;
using CrestApps.OrchardCore.Workforce.Services;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Workforce.ContactCenter.Services.Forecasting;

/// <summary>
/// Projects capacity plans: the work at the start from the plan or the planning group's history, and the weeks.
/// </summary>
public sealed class CapacityPlanService
{
    /// <summary>
    /// How many days of history give the work at the start when the plan doesn't set it.
    /// </summary>
    public const int HistoryDays = 28;

    private readonly PlanningGroupManager _groupManager;
    private readonly ForecastService _forecastService;
    private readonly IWorkerManager _workerManager;
    private readonly IClock _clock;

    /// <summary>
    /// Initializes a new instance of the <see cref="CapacityPlanService"/> class.
    /// </summary>
    /// <param name="groupManager">The planning group manager.</param>
    /// <param name="forecastService">The forecast service.</param>
    /// <param name="workerManager">The worker manager.</param>
    /// <param name="clock">The clock.</param>
    public CapacityPlanService(PlanningGroupManager groupManager, ForecastService forecastService, IWorkerManager workerManager, IClock clock)
    {
        _groupManager = groupManager;
        _forecastService = forecastService;
        _workerManager = workerManager;
        _clock = clock;
    }

    /// <summary>
    /// Projects a plan.
    /// </summary>
    /// <param name="plan">The plan.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The projection; it has no weeks when the planning group is gone or the work at the start is unknown.</returns>
    public async Task<CapacityProjection> ProjectAsync(CapacityPlan plan, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(plan);

        var projection = new CapacityProjection { Plan = plan, Today = DateOnly.FromDateTime(_clock.UtcNow) };

        projection.Group = string.IsNullOrEmpty(plan.PlanningGroupId) ? null : await _groupManager.FindByIdAsync(plan.PlanningGroupId, cancellationToken);

        if (projection.Group is null)
        {
            return projection;
        }

        projection.WorkloadHours = plan.WeeklyWorkloadHours;

        if (projection.WorkloadHours is null)
        {
            projection.WorkloadHours = await HistoryWorkloadAsync(projection.Group, cancellationToken);
            projection.WorkloadFromHistory = projection.WorkloadHours.HasValue;
        }

        if (projection.WorkloadHours.HasValue)
        {
            projection.Weeks = CapacityPlanner.Project(plan, projection.WorkloadHours.Value, projection.Group.ShrinkagePercent, projection.Group.MaxOccupancyPercent);
        }

        return projection;
    }

    /// <summary>
    /// Gives the handle hours of an average week of the planning group's last four weeks, or <see langword="null"/>
    /// when they had no handled work.
    /// </summary>
    /// <param name="group">The planning group.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The hours.</returns>
    public async Task<double?> HistoryWorkloadAsync(PlanningGroup group, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(group);

        var today = DateOnly.FromDateTime(_clock.UtcNow);
        var points = await _forecastService.HistoryAsync(group, today.AddDays(-HistoryDays), today.AddDays(-1), cancellationToken);
        var handled = points.Sum(point => point.Handled);

        if (handled <= 0)
        {
            return null;
        }

        // Every contact offered is work, at the average handle time of those handled.
        var averageHandleSeconds = points.Sum(point => point.HandleSeconds) / handled;
        var weeks = HistoryDays / 7d;

        return points.Sum(point => point.Volume) * averageHandleSeconds / 3600d / weeks / Math.Max(1, group.Concurrency);
    }

    /// <summary>
    /// Counts the workers of the planning group's team today.
    /// </summary>
    /// <param name="group">The planning group.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The headcount.</returns>
    public async Task<int> TeamHeadcountAsync(PlanningGroup group, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(group);

        if (string.IsNullOrEmpty(group.TeamId))
        {
            return 0;
        }

        return (await _workerManager.ListByTeamAsync(group.TeamId, DateOnly.FromDateTime(_clock.UtcNow), cancellationToken)).Count;
    }
}

/// <summary>
/// A capacity plan worked out week by week.
/// </summary>
public sealed class CapacityProjection
{
    /// <summary>
    /// Gets or sets the plan.
    /// </summary>
    public CapacityPlan Plan { get; set; }

    /// <summary>
    /// Gets or sets the day the plan was projected.
    /// </summary>
    public DateOnly Today { get; set; }

    /// <summary>
    /// Gets or sets the planning group, or <see langword="null"/> when it is gone.
    /// </summary>
    public PlanningGroup Group { get; set; }

    /// <summary>
    /// Gets or sets the handle hours of a week of work at the start, or <see langword="null"/> when unknown.
    /// </summary>
    public double? WorkloadHours { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the work at the start comes from the last four weeks.
    /// </summary>
    public bool WorkloadFromHistory { get; set; }

    /// <summary>
    /// Gets or sets the weeks.
    /// </summary>
    public IReadOnlyList<CapacityWeek> Weeks { get; set; } = [];

    /// <summary>
    /// Gets the first week short of agents, or <see langword="null"/>.
    /// </summary>
    public CapacityWeek FirstShortWeek => Weeks.FirstOrDefault(week => week.Gap < -0.05);

    /// <summary>
    /// Gets the week most short of agents, or <see langword="null"/> when none is.
    /// </summary>
    public CapacityWeek WorstWeek => Weeks.Where(week => week.Gap < -0.05).OrderBy(week => week.Gap).FirstOrDefault();
}
