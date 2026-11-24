using CrestApps.Core;
using CrestApps.OrchardCore.Staff.Core.Services;
using CrestApps.OrchardCore.Staff.Models;
using CrestApps.OrchardCore.Workforce.Models;
using CrestApps.OrchardCore.Workforce.Services;
using Microsoft.Extensions.Localization;
using OrchardCore.Modules;
using YesSql;

namespace CrestApps.OrchardCore.Workforce.Core.Services;

/// <summary>
/// The default <see cref="ITimeOffBalanceService"/>.
/// </summary>
internal sealed class DefaultTimeOffBalanceService : ITimeOffBalanceService
{
    private static readonly TimeOffLedgerKind[] _manualKinds = [TimeOffLedgerKind.Opening, TimeOffLedgerKind.Adjustment, TimeOffLedgerKind.Payout, TimeOffLedgerKind.Carryover, TimeOffLedgerKind.Grant];

    private readonly StaffMemberStore _workerStore;
    private readonly TimeOffPolicyStore _policyStore;
    private readonly TimeOffTypeStore _typeStore;
    private readonly TimeOffLedgerStore _ledgerStore;
    private readonly TimeOffRequestStore _requestStore;
    private readonly TimeOffPolicyResolvers _resolvers;
    private readonly TimeOffAccrualPlanner _planner;
    private readonly TimeOffLocks _locks;
    private readonly ISession _session;
    private readonly IClock _clock;

    internal readonly IStringLocalizer S;

    public DefaultTimeOffBalanceService(
        StaffMemberStore workerStore,
        TimeOffPolicyStore policyStore,
        TimeOffTypeStore typeStore,
        TimeOffLedgerStore ledgerStore,
        TimeOffRequestStore requestStore,
        TimeOffPolicyResolvers resolvers,
        TimeOffAccrualPlanner planner,
        TimeOffLocks locks,
        ISession session,
        IClock clock,
        IStringLocalizer<DefaultTimeOffBalanceService> stringLocalizer)
    {
        _workerStore = workerStore;
        _policyStore = policyStore;
        _typeStore = typeStore;
        _ledgerStore = ledgerStore;
        _requestStore = requestStore;
        _resolvers = resolvers;
        _planner = planner;
        _locks = locks;
        _session = session;
        _clock = clock;
        S = stringLocalizer;
    }

    public async Task<IReadOnlyList<TimeOffBalance>> GetBalancesAsync(string workerId, DateOnly date, CancellationToken cancellationToken = default)
    {
        var worker = await _workerStore.FindByIdAsync(workerId, cancellationToken);

        if (worker is null)
        {
            return [];
        }

        var resolver = await _resolvers.ForWorkerAsync(worker, cancellationToken);
        var balances = new List<TimeOffBalance>();

        foreach (var policy in resolver.ListOn(date))
        {
            balances.Add(await ComputeAsync(worker, policy, resolver, date, cancellationToken));
        }

        return balances;
    }

    public async Task<TimeOffBalance> GetBalanceAsync(string workerId, string policyId, DateOnly date, CancellationToken cancellationToken = default)
    {
        var worker = await _workerStore.FindByIdAsync(workerId, cancellationToken);
        var policy = string.IsNullOrEmpty(policyId) ? null : await _policyStore.FindByIdAsync(policyId, cancellationToken);

        if (worker is null || policy is null)
        {
            return null;
        }

        return await ComputeAsync(worker, policy, await _resolvers.ForWorkerAsync(worker, cancellationToken), date, cancellationToken);
    }

    public Task<(int Count, IReadOnlyList<TimeOffLedgerEntry> Items)> PageLedgerAsync(string workerId, string policyId, int skip, int take, CancellationToken cancellationToken = default)
    {
        return _ledgerStore.PageAsync(workerId, policyId, skip, take, cancellationToken);
    }

    public async Task<PlanningResult<TimeOffLedgerEntry>> AdjustAsync(TimeOffAdjustment adjustment, string actor, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(adjustment);

        var result = new PlanningResult<TimeOffLedgerEntry>();
        var worker = await _workerStore.FindByIdAsync(adjustment.WorkerId, cancellationToken);
        var policy = string.IsNullOrEmpty(adjustment.PolicyId) ? null : await _policyStore.FindByIdAsync(adjustment.PolicyId, cancellationToken);

        if (worker is null)
        {
            result.Fail(S["The worker doesn't exist."]);
        }

        if (policy is null)
        {
            result.Fail(S["The policy doesn't exist."]);
        }

        if (!_manualKinds.Contains(adjustment.Kind))
        {
            result.Fail(S["A balance can be changed by hand only with an opening balance, an adjustment, a carryover, a grant or a payout."]);
        }

        if (adjustment.Kind == TimeOffLedgerKind.Grant)
        {
            if (adjustment.Amount <= 0)
            {
                result.Fail(S["A grant adds hours or days; enter an amount above 0."]);
            }

            if (adjustment.ExpiresOn is not { } last || last < adjustment.EffectiveDate)
            {
                result.Fail(S["Give the grant the last day it can be used, on or after the day it starts."]);
            }
        }
        else if (adjustment.Amount == 0)
        {
            result.Fail(S["Enter an amount other than 0."]);
        }

        if (!result.Succeeded)
        {
            return result;
        }

        var key = string.IsNullOrWhiteSpace(adjustment.IdempotencyKey) ? null : "adjust:" + adjustment.IdempotencyKey.Trim();
        var lockers = await _locks.LockAsync([TimeOffLocks.Worker(worker.ItemId)]);

        if (lockers is null)
        {
            result.Fail(S["Someone else is changing {0}'s time off. Try again.", worker.DisplayName]);

            return result;
        }

        try
        {
            if (key is not null && await _ledgerStore.FindByKeyAsync(key, cancellationToken) is { } existing)
            {
                result.Value = existing;

                return result;
            }

            var entry = new TimeOffLedgerEntry
            {
                ItemId = UniqueId.GenerateId(),
                WorkerId = worker.ItemId,
                PolicyId = policy.ItemId,
                Kind = adjustment.Kind,
                Amount = adjustment.Kind == TimeOffLedgerKind.Payout ? -Math.Abs(adjustment.Amount) : adjustment.Amount,
                EffectiveDate = adjustment.EffectiveDate,
                ExpiresOn = adjustment.Kind == TimeOffLedgerKind.Grant ? adjustment.ExpiresOn : null,
                IdempotencyKey = key ?? "adjust:" + UniqueId.GenerateId(),
                Note = adjustment.Note?.Trim(),
                Actor = actor,
                CreatedUtc = _clock.UtcNow,
            };

            await _ledgerStore.CreateAsync(entry, cancellationToken);
            await _session.SaveChangesAsync(cancellationToken);
            result.Value = entry;
        }
        finally
        {
            await TimeOffLocks.ReleaseAsync(lockers);
        }

        return result;
    }

    private async Task<TimeOffBalance> ComputeAsync(StaffMember worker, TimeOffPolicy policy, TimeOffPolicyResolver resolver, DateOnly date, CancellationToken cancellationToken)
    {
        var entries = await _ledgerStore.ListAsync(worker.ItemId, policy.ItemId, cancellationToken);
        var input = await _planner.BuildAsync(worker, policy, resolver, entries, date, cancellationToken);
        var open = await _requestStore.ListOpenForWorkerAsync(worker.ItemId, policy.ItemId, cancellationToken);
        var grants = TimeOffGrants.Allocate(entries).Where(lot => lot.IsGrant).ToList();

        return new TimeOffBalance
        {
            Policy = policy,
            Type = await _typeStore.FindByIdAsync(policy.TypeId, cancellationToken),
            Date = date,
            Current = entries.Where(entry => entry.EffectiveDate <= date).Sum(entry => entry.Amount),
            ProjectedAccrual = TimeOffAccrualEngine.Compute(input).Sum(entry => entry.Amount),
            Upcoming = -entries.Where(entry => entry.EffectiveDate > date && entry.Kind is TimeOffLedgerKind.Taken or TimeOffLedgerKind.Reversal).Sum(entry => entry.Amount),
            Pending = open.Sum(request => request.Amount),
            Expiring = grants.Where(lot => lot.ExpiresOn < date).Sum(lot => lot.Left),
            Grants = [.. grants
                .Where(lot => lot.ExpiresOn >= date && lot.Left > 0)
                .OrderBy(lot => lot.ExpiresOn)
                .Select(lot => new TimeOffGrantBalance
                {
                    EntryId = lot.Entry.ItemId,
                    Amount = lot.Entry.Amount,
                    Remaining = lot.Left,
                    EffectiveDate = lot.Entry.EffectiveDate,
                    ExpiresOn = lot.ExpiresOn.Value,
                    Note = lot.Entry.Note,
                })],
        };
    }
}
