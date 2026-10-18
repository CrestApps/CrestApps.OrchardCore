using CrestApps.OrchardCore.ContactCenter.Core.Indexes;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Indexes;
using CrestApps.OrchardCore.ContactCenter.Reports.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OrchardCore.Modules;
using YesSql;
using YesSql.Services;

namespace CrestApps.OrchardCore.ContactCenter.Reports.Services;

/// <summary>
/// Keeps the daily agent time summaries up to date: it computes each UTC day once the day is over, backfills the days
/// before the summaries existed, recomputes a day whose state changes the log no longer matches, and deletes the
/// summaries that fall out of the backfill window.
/// </summary>
/// <remarks>
/// It is meant to run from one place at a time, the background task, whose lock makes it single-writer across nodes.
/// A summary is never the only copy of anything: deleting one, or all of them, only makes the days they covered read
/// from the log until they are computed again.
/// </remarks>
internal sealed class AgentTimeSummaryRoller
{
    /// <summary>
    /// How long after midnight a day waits before it is summarized, so a sign-off dated by the agent's last heartbeat
    /// before midnight, which the sweep records a minute or two later, is in the log first.
    /// </summary>
    internal static readonly TimeSpan SettleDelay = TimeSpan.FromMinutes(30);

    /// <summary>
    /// The most days one run computes, so a backfill is spread across runs instead of holding one run for its length.
    /// </summary>
    internal const int MaxDaysPerRun = 10;

    /// <summary>
    /// The most recent days every run checks against the log, plus as many older ones in rotation.
    /// </summary>
    internal const int DaysVerifiedPerRun = 7;

    private const int MaxDeletionsPerRun = 50;

    private static readonly TimeSpan _rotationStep = TimeSpan.FromMinutes(5);

    private readonly ISession _session;
    private readonly IInteractionEventStore _eventStore;
    private readonly IAgentProfileManager _agentManager;
    private readonly ContactCenterReportingOptions _options;
    private readonly IClock _clock;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="AgentTimeSummaryRoller"/> class.
    /// </summary>
    /// <param name="session">The YesSql session the summaries are written through.</param>
    /// <param name="eventStore">The event log.</param>
    /// <param name="agentManager">The agent directory, whose agents' state before each day is looked up.</param>
    /// <param name="options">The reporting options that set the backfill window.</param>
    /// <param name="clock">The clock.</param>
    /// <param name="logger">The logger.</param>
    public AgentTimeSummaryRoller(
        ISession session,
        IInteractionEventStore eventStore,
        IAgentProfileManager agentManager,
        IOptions<ContactCenterReportingOptions> options,
        IClock clock,
        ILogger<AgentTimeSummaryRoller> logger)
    {
        _session = session;
        _eventStore = eventStore;
        _agentManager = agentManager;
        _options = options.Value;
        _clock = clock;
        _logger = logger;
    }

    /// <summary>
    /// Brings the summaries up to date by one bounded step.
    /// </summary>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>How many days were computed or deleted.</returns>
    public async Task<int> RollAsync(CancellationToken cancellationToken = default)
    {
        var now = _clock.UtcNow;
        var changes = await DeleteExpiredAsync(now, cancellationToken);
        var backfill = _options.AgentTimeSummaryBackfill;

        if (backfill <= TimeSpan.Zero)
        {
            return changes;
        }

        // The latest day that has been over for at least the settle delay, and the earliest the window keeps.
        var latestDay = AgentDailyTimeCalculator.DayStart(now - SettleDelay).AddDays(-1);
        var earliestDay = AgentDailyTimeCalculator.DayStart(now - backfill);
        var firstChange = await FirstStateChangeUtcAsync(cancellationToken);

        if (firstChange is null)
        {
            return changes;
        }

        var firstChangeDay = AgentDailyTimeCalculator.DayStart(firstChange.Value);

        if (firstChangeDay > earliestDay)
        {
            earliestDay = firstChangeDay;
        }

        if (latestDay < earliestDay)
        {
            return changes;
        }

        var current = await LoadCurrentAsync(earliestDay, latestDay, cancellationToken);
        var stale = await FindStaleAsync(current, now, cancellationToken);

        if (stale.HasValue)
        {
            // A change late in a day is the state the next days open in, so they are recomputed with it.
            foreach (var day in current.Keys.Where(candidate => candidate >= stale.Value).ToArray())
            {
                await DeleteDayAsync(day, cancellationToken);
                current.Remove(day);
                changes++;
            }

            await _session.SaveChangesAsync(cancellationToken);
        }

        var missing = new List<DateTime>();

        for (var day = latestDay; day >= earliestDay && missing.Count < MaxDaysPerRun; day = day.AddDays(-1))
        {
            if (!current.ContainsKey(day))
            {
                missing.Add(day);
            }
        }

        if (missing.Count == 0)
        {
            return changes;
        }

        var agentIds = (await _agentManager.GetAllAsync(cancellationToken))
            .Select(agent => agent.ItemId)
            .Where(agentId => !string.IsNullOrEmpty(agentId))
            .ToArray();

        foreach (var day in missing)
        {
            var summary = await AgentDailyTimeCalculator.ComputeDayAsync(_eventStore, agentIds, day, _clock.UtcNow, cancellationToken);

            await DeleteDayAsync(day, cancellationToken);
            await _session.SaveAsync(summary, collection: ContactCenterStorage.CollectionName, cancellationToken: cancellationToken);
            await _session.SaveChangesAsync(cancellationToken);

            changes++;
        }

        if (_logger.IsEnabled(LogLevel.Debug))
        {
            _logger.LogDebug("Computed the agent time summaries of {Count} day(s).", missing.Count);
        }

        return changes;
    }

    private async Task<DateTime?> FirstStateChangeUtcAsync(CancellationToken cancellationToken)
    {
        var types = AgentStateTimeline.StateEventTypes;

        var first = await _session.QueryIndex<InteractionEventIndex>(
                index => index.AggregateType == nameof(AgentProfile) && index.EventType.IsIn(types),
                collection: ContactCenterStorage.CollectionName)
            .OrderBy(index => index.OccurredUtc)
            .FirstOrDefaultAsync(cancellationToken);

        return first?.OccurredUtc;
    }

    private async Task<Dictionary<DateTime, AgentDailyTimeSummaryIndex>> LoadCurrentAsync(
        DateTime earliestDay,
        DateTime latestDay,
        CancellationToken cancellationToken)
    {
        var version = AgentDailyTimeCalculator.Version;

        var rows = await _session.QueryIndex<AgentDailyTimeSummaryIndex>(
                index => index.Date >= earliestDay && index.Date <= latestDay && index.Version == version,
                collection: ContactCenterStorage.CollectionName)
            .ListAsync(cancellationToken);

        return rows
            .GroupBy(row => AgentDailyTimeCalculator.DayStart(row.Date))
            .ToDictionary(group => group.Key, group => group.OrderByDescending(row => row.DocumentId).First());
    }

    // Checks the most recent days and a rotating slice of the older ones against the log, and returns the earliest day
    // whose summary no longer matches it.
    private async Task<DateTime?> FindStaleAsync(
        Dictionary<DateTime, AgentDailyTimeSummaryIndex> current,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var days = current.Keys.OrderBy(candidate => candidate).ToArray();

        if (days.Length == 0)
        {
            return null;
        }

        var verified = new SortedSet<DateTime>(days.OrderByDescending(candidate => candidate).Take(DaysVerifiedPerRun));
        var older = days.Length - verified.Count;

        if (older > 0)
        {
            var slices = (older + DaysVerifiedPerRun - 1) / DaysVerifiedPerRun;
            var slice = (int)(now.Ticks / _rotationStep.Ticks % slices);

            foreach (var day in days.Take(older).Skip(slice * DaysVerifiedPerRun).Take(DaysVerifiedPerRun))
            {
                verified.Add(day);
            }
        }

        foreach (var day in verified)
        {
            var count = await AgentDailyTimeCalculator.CountStateEventsAsync(_session, day, day.AddDays(1).AddTicks(-1), cancellationToken);

            if (count != current[day].EventCount)
            {
                if (_logger.IsEnabled(LogLevel.Information))
                {
                    _logger.LogInformation(
                        "The agent time summary of {Day:yyyy-MM-dd} was computed from {Expected} state changes but the log now holds {Actual}; it and the days after it are recomputed.",
                        day,
                        current[day].EventCount,
                        count);
                }

                return day;
            }
        }

        return null;
    }

    private async Task<int> DeleteExpiredAsync(DateTime now, CancellationToken cancellationToken)
    {
        var backfill = _options.AgentTimeSummaryBackfill;

        // With the summaries turned off none is read, so none is kept either.
        var cutoff = backfill <= TimeSpan.Zero
            ? AgentDailyTimeCalculator.DayStart(now).AddDays(1)
            : AgentDailyTimeCalculator.DayStart(now - backfill);

        var expired = await _session.Query<AgentDailyTimeSummary, AgentDailyTimeSummaryIndex>(
                index => index.Date < cutoff,
                collection: ContactCenterStorage.CollectionName)
            .Take(MaxDeletionsPerRun)
            .ListAsync(cancellationToken);

        var deleted = 0;

        foreach (var summary in expired)
        {
            _session.Delete(summary, ContactCenterStorage.CollectionName);
            deleted++;
        }

        if (deleted > 0)
        {
            await _session.SaveChangesAsync(cancellationToken);
        }

        return deleted;
    }

    private async Task DeleteDayAsync(DateTime day, CancellationToken cancellationToken)
    {
        var summaries = await _session.Query<AgentDailyTimeSummary, AgentDailyTimeSummaryIndex>(
                index => index.Date == day,
                collection: ContactCenterStorage.CollectionName)
            .ListAsync(cancellationToken);

        foreach (var summary in summaries)
        {
            _session.Delete(summary, ContactCenterStorage.CollectionName);
        }
    }
}
