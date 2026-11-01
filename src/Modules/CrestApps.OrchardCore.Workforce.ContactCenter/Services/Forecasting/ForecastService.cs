using CrestApps.OrchardCore.Calendars.Core;
using CrestApps.OrchardCore.ContactCenter.Core.IntervalStatistics;
using CrestApps.OrchardCore.Workforce.ContactCenter.Indexes;
using CrestApps.OrchardCore.Workforce.ContactCenter.Models;
using CrestApps.OrchardCore.Workforce.Models;
using CrestApps.OrchardCore.Workforce.Services;
using OrchardCore;
using OrchardCore.Modules;
using YesSql;

namespace CrestApps.OrchardCore.Workforce.ContactCenter.Services.Forecasting;

/// <summary>
/// Makes forecasts of planning groups from their interval statistics, staffs them with Erlang C or A, and turns the
/// active forecast into the staffing team's coverage.
/// </summary>
public sealed class ForecastService
{
    /// <summary>
    /// The name of the coverage requirement a group's active forecast writes, before the group's name.
    /// </summary>
    public const string CoverageNamePrefix = "Forecast: ";

    private readonly ISession _session;
    private readonly PlanningGroupManager _groupManager;
    private readonly ForecastEventManager _eventManager;
    private readonly IIntervalStatisticsReader _statistics;
    private readonly ICoverageService _coverageService;
    private readonly IWorkerTimeZoneService _timeZones;
    private readonly IClock _clock;

    /// <summary>
    /// Initializes a new instance of the <see cref="ForecastService"/> class.
    /// </summary>
    /// <param name="session">The session.</param>
    /// <param name="groupManager">The planning group manager.</param>
    /// <param name="eventManager">The forecast event manager.</param>
    /// <param name="statistics">The interval statistics.</param>
    /// <param name="coverageService">The coverage service.</param>
    /// <param name="timeZones">The time zones.</param>
    /// <param name="clock">The clock.</param>
    public ForecastService(
        ISession session,
        PlanningGroupManager groupManager,
        ForecastEventManager eventManager,
        IIntervalStatisticsReader statistics,
        ICoverageService coverageService,
        IWorkerTimeZoneService timeZones,
        IClock clock)
    {
        _session = session;
        _groupManager = groupManager;
        _eventManager = eventManager;
        _statistics = statistics;
        _coverageService = coverageService;
        _timeZones = timeZones;
        _clock = clock;
    }

    /// <summary>
    /// Reads a planning group's history in its time zone.
    /// </summary>
    /// <param name="group">The planning group.</param>
    /// <param name="fromDate">The first day of history.</param>
    /// <param name="toDate">The last day of history.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The intervals with work.</returns>
    public async Task<List<ForecastHistoryPoint>> HistoryAsync(PlanningGroup group, DateOnly fromDate, DateOnly toDate, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(group);

        var zone = await _timeZones.GetTeamAsync(group.TeamId, cancellationToken);
        var fromUtc = CalendarTime.ToUtc(fromDate.ToDateTime(TimeOnly.MinValue), zone);
        var toUtc = CalendarTime.ToUtc(toDate.AddDays(1).ToDateTime(TimeOnly.MinValue), zone);
        var intervals = await _statistics.GetQueueIntervalsAsync(group.QueueIds, fromUtc, toUtc, cancellationToken);

        return [.. intervals
            .Where(interval => group.Channel is null || interval.Channel == group.Channel)
            .Select(interval => (Local: CalendarTime.ToLocal(interval.IntervalStartUtc, zone), Interval: interval))
            .GroupBy(entry => (Date: DateOnly.FromDateTime(entry.Local), Slot: ((entry.Local.Hour * 60) + entry.Local.Minute) / 15))
            .Select(group => new ForecastHistoryPoint
            {
                Date = group.Key.Date,
                Slot = group.Key.Slot,
                Volume = group.Sum(entry => entry.Interval.Offered),
                Handled = group.Sum(entry => entry.Interval.Handled),
                HandleSeconds = group.Sum(entry => entry.Interval.HandleSeconds),
            })
            .OrderBy(point => point.Date)
            .ThenBy(point => point.Slot)];
    }

    /// <summary>
    /// Makes a draft forecast of a planning group, staffed interval by interval.
    /// </summary>
    /// <param name="groupId">The planning group.</param>
    /// <param name="fromDate">The first day.</param>
    /// <param name="toDate">The last day.</param>
    /// <param name="model">The model; <see cref="ForecastModelKind.Auto"/> picks the best on the last two weeks.</param>
    /// <param name="actor">Who makes it.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The forecast, saved as a draft; or the errors.</returns>
    public async Task<PlanningResult<Forecast>> CreateAsync(string groupId, DateOnly fromDate, DateOnly toDate, ForecastModelKind model, string actor, CancellationToken cancellationToken = default)
    {
        var result = new PlanningResult<Forecast>();
        var group = string.IsNullOrEmpty(groupId) ? null : await _groupManager.FindByIdAsync(groupId, cancellationToken);

        if (group is null)
        {
            result.Fail("The planning group doesn't exist.");

            return result;
        }

        if (toDate < fromDate || toDate.DayNumber - fromDate.DayNumber > 92)
        {
            result.Fail("A forecast covers 1 to 93 days.");

            return result;
        }

        var history = await HistoryAsync(group, fromDate.AddDays(-7 * group.HistoryWeeks), fromDate.AddDays(-1), cancellationToken);

        if (history.Count == 0)
        {
            result.Fail("The planning group's queues have no interval statistics in the weeks before the first day.");

            return result;
        }

        var events = (await _eventManager.GetAllAsync(cancellationToken))
            .Where(item => item.PlanningGroupIds.Count == 0 || item.PlanningGroupIds.Contains(group.ItemId))
            .ToArray();
        var backtests = new List<ForecastBacktest>();

        foreach (var kind in new[] { ForecastModelKind.WeightedHistory, ForecastModelKind.HoltWinters })
        {
            var backtest = ForecastEngine.Backtest(history, kind, 2, group.HistoryWeeks);

            if (!double.IsInfinity(backtest.Wape))
            {
                backtests.Add(new ForecastBacktest { Model = kind, Mape = backtest.Mape, Wape = backtest.Wape });
            }
        }

        if (model == ForecastModelKind.Auto)
        {
            model = ForecastEngine.ChooseModel(history, 2, group.HistoryWeeks);
        }

        var zone = await _timeZones.GetTeamAsync(group.TeamId, cancellationToken);
        var forecast = new Forecast
        {
            ItemId = IdGenerator.GenerateId(),
            PlanningGroupId = group.ItemId,
            FromDate = fromDate,
            ToDate = toDate,
            TimeZoneId = zone,
            Model = model,
            Status = ForecastStatus.Draft,
            Backtests = backtests,
            ShrinkagePercent = group.ShrinkagePercent,
            CreatedBy = actor,
            CreatedUtc = _clock.UtcNow,
        };

        foreach (var point in ForecastEngine.Build(history, fromDate, toDate, model, events, group.HistoryWeeks).Where(point => point.Volume > 0))
        {
            var local = point.Date.ToDateTime(TimeOnly.MinValue).AddMinutes(point.Slot * 15);

            forecast.Intervals.Add(Staff(group, new ForecastInterval
            {
                Date = point.Date,
                Slot = point.Slot,
                StartUtc = CalendarTime.ToUtc(local, zone),
                Volume = point.Volume,
                AverageHandleSeconds = point.AverageHandleSeconds,
            }));
        }

        await _session.SaveAsync(forecast, collection: WorkforceConstants.CollectionName, cancellationToken: cancellationToken);

        result.Value = forecast;

        return result;
    }

    /// <summary>
    /// Changes a forecast's volume by a percentage over some days and staffs those days again.
    /// </summary>
    /// <param name="forecast">The forecast.</param>
    /// <param name="adjustment">The adjustment.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The errors, or none.</returns>
    public async Task<IReadOnlyList<string>> AdjustAsync(Forecast forecast, ForecastAdjustment adjustment, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(forecast);
        ArgumentNullException.ThrowIfNull(adjustment);

        if (adjustment.ToDate < adjustment.FromDate || adjustment.Percent <= -100)
        {
            return ["An adjustment needs its first day before its last, and can't take away all the work."];
        }

        var group = string.IsNullOrEmpty(forecast.PlanningGroupId) ? null : await _groupManager.FindByIdAsync(forecast.PlanningGroupId, cancellationToken);

        if (group is null)
        {
            return ["The planning group doesn't exist."];
        }

        foreach (var interval in forecast.Intervals.Where(interval => interval.Date >= adjustment.FromDate && interval.Date <= adjustment.ToDate))
        {
            interval.Volume *= 1 + (adjustment.Percent / 100d);
            Staff(group, interval);
        }

        forecast.Adjustments.Add(adjustment);

        await _session.SaveAsync(forecast, collection: WorkforceConstants.CollectionName, cancellationToken: cancellationToken);

        if (forecast.Status == ForecastStatus.Active)
        {
            await WriteCoverageAsync(group, forecast, cancellationToken);
        }

        return [];
    }

    /// <summary>
    /// Makes a forecast its group's active forecast: the one before is superseded, and the staffing becomes the
    /// staffing team's coverage requirement for the forecast's days.
    /// </summary>
    /// <param name="forecast">The forecast.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The errors, or none.</returns>
    public async Task<IReadOnlyList<string>> ActivateAsync(Forecast forecast, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(forecast);

        var group = string.IsNullOrEmpty(forecast.PlanningGroupId) ? null : await _groupManager.FindByIdAsync(forecast.PlanningGroupId, cancellationToken);

        if (group is null)
        {
            return ["The planning group doesn't exist."];
        }

        if (string.IsNullOrEmpty(group.TeamId))
        {
            return ["Give the planning group the team that staffs it first."];
        }

        var active = ForecastStatus.Active.ToString();

        foreach (var previous in await _session.Query<Forecast, ForecastIndex>(index => index.PlanningGroupId == forecast.PlanningGroupId && index.Status == active, collection: WorkforceConstants.CollectionName).ListAsync(cancellationToken))
        {
            if (previous.ItemId != forecast.ItemId)
            {
                previous.Status = ForecastStatus.Superseded;
                await _session.SaveAsync(previous, collection: WorkforceConstants.CollectionName, cancellationToken: cancellationToken);
            }
        }

        forecast.Status = ForecastStatus.Active;
        forecast.ActivatedUtc = _clock.UtcNow;

        await _session.SaveAsync(forecast, collection: WorkforceConstants.CollectionName, cancellationToken: cancellationToken);

        return await WriteCoverageAsync(group, forecast, cancellationToken);
    }

    /// <summary>
    /// Finds a forecast.
    /// </summary>
    /// <param name="id">The identifier.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The forecast, or <see langword="null"/>.</returns>
    public Task<Forecast> FindAsync(string id, CancellationToken cancellationToken = default)
        => _session.Query<Forecast, ForecastIndex>(index => index.ItemId == id, collection: WorkforceConstants.CollectionName).FirstOrDefaultAsync(cancellationToken);

    /// <summary>
    /// Gets the forecasts, newest first, a page at a time.
    /// </summary>
    /// <param name="groupId">The planning group, or <see langword="null"/> for every group.</param>
    /// <param name="skip">How many to skip.</param>
    /// <param name="take">How many to take.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The count and the page.</returns>
    public async Task<(int Count, IReadOnlyList<Forecast> Items)> PageAsync(string groupId, int skip, int take, CancellationToken cancellationToken = default)
    {
        var query = string.IsNullOrEmpty(groupId)
            ? _session.Query<Forecast, ForecastIndex>(collection: WorkforceConstants.CollectionName)
            : _session.Query<Forecast, ForecastIndex>(index => index.PlanningGroupId == groupId, collection: WorkforceConstants.CollectionName);

        var count = await query.CountAsync(cancellationToken);
        var items = await query.OrderByDescending(index => index.CreatedUtc).Skip(skip).Take(take).ListAsync(cancellationToken);

        return (count, [.. items]);
    }

    /// <summary>
    /// Gets the active forecast intervals of a planning group over a stretch of time.
    /// </summary>
    /// <param name="groupId">The planning group.</param>
    /// <param name="fromUtc">The start.</param>
    /// <param name="toUtc">The end.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The intervals, in order.</returns>
    public async Task<IReadOnlyList<ForecastInterval>> ActiveIntervalsAsync(string groupId, DateTime fromUtc, DateTime toUtc, CancellationToken cancellationToken = default)
    {
        var active = ForecastStatus.Active.ToString();
        var forecasts = await _session.Query<Forecast, ForecastIndex>(index => index.PlanningGroupId == groupId && index.Status == active, collection: WorkforceConstants.CollectionName).ListAsync(cancellationToken);

        return [.. forecasts
            .SelectMany(forecast => forecast.Intervals)
            .Where(interval => interval.StartUtc >= fromUtc && interval.StartUtc < toUtc)
            .OrderBy(interval => interval.StartUtc)];
    }

    /// <summary>
    /// Removes a forecast; an active one's coverage stays until another forecast is activated.
    /// </summary>
    /// <param name="forecast">The forecast.</param>
    public void Remove(Forecast forecast)
        => _session.Delete(forecast, WorkforceConstants.CollectionName);

    /// <summary>
    /// Staffs an interval for a planning group's goal and shrinkage.
    /// </summary>
    /// <param name="group">The planning group.</param>
    /// <param name="interval">The interval.</param>
    /// <returns>The interval, staffed.</returns>
    public static ForecastInterval Staff(PlanningGroup group, ForecastInterval interval)
    {
        ArgumentNullException.ThrowIfNull(group);
        ArgumentNullException.ThrowIfNull(interval);

        var staffing = ErlangCalculator.Staff(new StaffingRequest
        {
            Volume = interval.Volume,
            IntervalSeconds = 900,
            AverageHandleSeconds = interval.AverageHandleSeconds,
            ServiceLevelPercent = group.ServiceLevelPercent,
            ServiceLevelSeconds = group.ServiceLevelSeconds,
            AverageSpeedOfAnswerSeconds = group.AverageSpeedOfAnswerSeconds,
            MaxOccupancyPercent = group.MaxOccupancyPercent,
            Concurrency = group.Concurrency,
            PatienceSeconds = group.PatienceSeconds,
        });

        interval.RequiredAgents = staffing.Agents;
        interval.RequiredScheduled = staffing.Agents == 0 ? 0 : (int)Math.Ceiling(staffing.Agents / (1 - (group.ShrinkagePercent / 100d)) - 1e-9);
        interval.ServiceLevel = staffing.ServiceLevel;
        interval.AverageSpeedOfAnswerSeconds = double.IsInfinity(staffing.AverageSpeedOfAnswerSeconds) ? 0 : staffing.AverageSpeedOfAnswerSeconds;
        interval.Occupancy = staffing.Occupancy;

        return interval;
    }

    private async Task<IReadOnlyList<string>> WriteCoverageAsync(PlanningGroup group, Forecast forecast, CancellationToken cancellationToken)
    {
        var name = CoverageNamePrefix + group.Name;
        var requirement = (await _coverageService.ListRequirementsAsync(group.TeamId, cancellationToken))
            .FirstOrDefault(item => item.Source == CoverageSource.Forecast && string.Equals(item.Name, name, StringComparison.Ordinal))
            ?? new CoverageRequirement { TeamId = group.TeamId, Name = name, Source = CoverageSource.Forecast };

        requirement.EffectiveFrom = forecast.FromDate;
        requirement.EffectiveTo = forecast.ToDate;
        requirement.Rows = [.. Rows(forecast)];

        var saved = await _coverageService.SaveRequirementAsync(requirement, cancellationToken);

        return saved.Succeeded ? [] : [.. saved.Errors];
    }

    private static IEnumerable<CoverageRow> Rows(Forecast forecast)
    {
        // Consecutive intervals of a day that need the same people become one row.
        foreach (var day in forecast.Intervals.Where(interval => interval.RequiredAgents > 0).GroupBy(interval => interval.Date).OrderBy(group => group.Key))
        {
            CoverageRow row = null;
            var lastSlot = -2;

            foreach (var interval in day.OrderBy(interval => interval.Slot))
            {
                if (row is not null && interval.Slot == lastSlot + 1 && row.Minimum == interval.RequiredAgents && row.Target == interval.RequiredScheduled)
                {
                    row.End = SlotTime(interval.Slot + 1);
                }
                else
                {
                    if (row is not null)
                    {
                        yield return row;
                    }

                    row = new CoverageRow
                    {
                        Date = day.Key,
                        Start = SlotTime(interval.Slot),
                        End = SlotTime(interval.Slot + 1),
                        Minimum = interval.RequiredAgents,
                        Target = interval.RequiredScheduled,
                    };
                }

                lastSlot = interval.Slot;
            }

            if (row is not null)
            {
                yield return row;
            }
        }
    }

    private static TimeOnly SlotTime(int slot) => slot >= ForecastEngine.SlotsPerDay ? TimeOnly.MinValue : TimeOnly.FromTimeSpan(TimeSpan.FromMinutes(slot * 15));
}
