using CrestApps.OrchardCore.Workforce.Core.Indexes;
using CrestApps.OrchardCore.Workforce.Models;
using CrestApps.OrchardCore.Workforce.Services;
using Microsoft.Extensions.Logging;
using OrchardCore.Modules;
using YesSql;
using YesSql.Services;

namespace CrestApps.OrchardCore.Workforce.Core.Services;

/// <summary>
/// The default <see cref="ITimeOffAccrualService"/>. Each worker is accrued under their lock and saved before it is
/// let go, and every entry carries a key of the worker, the policy and its day, so a rerun writes nothing new.
/// </summary>
internal sealed class DefaultTimeOffAccrualService : ITimeOffAccrualService
{
    private readonly WorkerStore _workerStore;
    private readonly TeamStore _teamStore;
    private readonly TimeOffPolicyStore _policyStore;
    private readonly TimeOffLedgerStore _ledgerStore;
    private readonly TimeOffPolicyResolvers _resolvers;
    private readonly TimeOffAccrualPlanner _planner;
    private readonly TimeOffLocks _locks;
    private readonly ISession _session;
    private readonly IClock _clock;
    private readonly ILogger _logger;

    public DefaultTimeOffAccrualService(
        WorkerStore workerStore,
        TeamStore teamStore,
        TimeOffPolicyStore policyStore,
        TimeOffLedgerStore ledgerStore,
        TimeOffPolicyResolvers resolvers,
        TimeOffAccrualPlanner planner,
        TimeOffLocks locks,
        ISession session,
        IClock clock,
        ILogger<DefaultTimeOffAccrualService> logger)
    {
        _workerStore = workerStore;
        _teamStore = teamStore;
        _policyStore = policyStore;
        _ledgerStore = ledgerStore;
        _resolvers = resolvers;
        _planner = planner;
        _locks = locks;
        _session = session;
        _clock = clock;
        _logger = logger;
    }

    public async Task<TimeOffAccrualRun> RunAsync(DateOnly date, CancellationToken cancellationToken = default)
    {
        var policies = await _policyStore.ListAccruingAsync(cancellationToken);
        var workerIds = new HashSet<string>(StringComparer.Ordinal);
        var teamIds = new HashSet<string>(StringComparer.Ordinal);
        var teams = await _teamStore.ListAsync(cancellationToken);

        foreach (var assignment in policies.SelectMany(policy => policy.Assignments))
        {
            if (!string.IsNullOrEmpty(assignment.WorkerId))
            {
                workerIds.Add(assignment.WorkerId);
            }
            else if (!string.IsNullOrEmpty(assignment.TeamId))
            {
                teamIds.UnionWith(await TeamTree.GetSubtreeIdsAsync(teams, assignment.TeamId));
            }
        }

        if (teamIds.Count > 0)
        {
            var ids = teamIds.ToArray();
            var memberships = await _session.QueryIndex<WorkerTeamMembershipIndex>(index => index.TeamId.IsIn(ids), collection: WorkforceConstants.CollectionName)
                .ListAsync(cancellationToken);

            workerIds.UnionWith(memberships.Select(membership => membership.WorkerId));
        }

        var entries = 0;

        foreach (var workerId in workerIds.Order(StringComparer.Ordinal))
        {
            cancellationToken.ThrowIfCancellationRequested();
            entries += await RunForWorkerAsync(workerId, date, cancellationToken);
        }

        return new TimeOffAccrualRun(workerIds.Count, entries);
    }

    public async Task<int> RunForWorkerAsync(string workerId, DateOnly date, CancellationToken cancellationToken = default)
    {
        var lockers = await _locks.LockAsync([TimeOffLocks.Worker(workerId)]);

        if (lockers is null)
        {
            _logger.LogWarning("Time off accrual skipped a worker whose time off someone else is changing; the next run accrues them.");

            return 0;
        }

        try
        {
            var worker = await _workerStore.FindByIdAsync(workerId, cancellationToken);

            if (worker is null)
            {
                return 0;
            }

            var resolver = await _resolvers.ForWorkerAsync(worker, cancellationToken);
            var now = _clock.UtcNow;
            var written = 0;

            foreach (var policy in resolver.Policies.Where(policy => policy.Enabled && policy.AccrualMethod != TimeOffAccrualMethod.None))
            {
                var existing = await _ledgerStore.ListAsync(worker.ItemId, policy.ItemId, cancellationToken);
                var input = await _planner.BuildAsync(worker, policy, resolver, existing, date, cancellationToken);

                foreach (var entry in TimeOffAccrualEngine.Compute(input))
                {
                    entry.Actor = "Accrual";
                    entry.CreatedUtc = now;
                    await _ledgerStore.CreateAsync(entry, cancellationToken);
                    written++;
                }
            }

            if (written > 0)
            {
                // Saved before the lock is let go, so a run at the same time finds these entries.
                await _session.SaveChangesAsync(cancellationToken);
            }

            return written;
        }
        finally
        {
            await TimeOffLocks.ReleaseAsync(lockers);
        }
    }
}
