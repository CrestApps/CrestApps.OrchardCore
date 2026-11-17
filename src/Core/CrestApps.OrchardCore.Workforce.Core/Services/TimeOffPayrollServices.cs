using CrestApps.Core;
using CrestApps.OrchardCore.Payroll.Models;
using CrestApps.OrchardCore.Payroll.Services;
using CrestApps.OrchardCore.Staff.Core.Services;
using CrestApps.OrchardCore.Staff.Models;
using CrestApps.OrchardCore.Workforce.Models;
using CrestApps.OrchardCore.Workforce.Services;

namespace CrestApps.OrchardCore.Workforce.Core.Services;

/// <summary>
/// The pay runs and exports that took a time-off payout, stored on its ledger entry.
/// </summary>
internal sealed class TimeOffPayoutConsumers
{
    /// <summary>
    /// Gets or sets the ids of the pay runs or exports that took it.
    /// </summary>
    public List<string> ConsumerIds { get; set; } = [];
}

/// <summary>
/// Hands time-off payouts to payroll: each payout recorded on a balance (the ledger's <see cref="TimeOffLedgerKind.Payout"/>
/// kind, such as what is left at the end of employment) on or before the period's last day becomes hours on the
/// time-off type's pay code (else the first pay code of kind Paid time off), at the staff member's rate. A pay run or an
/// export that takes one marks it, so it is paid once; a voided run gives it back.
/// </summary>
internal sealed class TimeOffPayoutPayInputSource : IPayInputSource
{
    /// <summary>
    /// The kind of record a payout comes from.
    /// </summary>
    public const string Kind = "TimeOffPayout";

    private readonly TimeOffLedgerStore _ledger;
    private readonly TimeOffPolicyStore _policies;
    private readonly TimeOffTypeStore _types;
    private readonly StaffMemberStore _workers;
    private readonly IPayCodeManager _codes;

    public TimeOffPayoutPayInputSource(TimeOffLedgerStore ledger, TimeOffPolicyStore policies, TimeOffTypeStore types, StaffMemberStore workers, IPayCodeManager codes)
    {
        _ledger = ledger;
        _policies = policies;
        _types = types;
        _workers = workers;
        _codes = codes;
    }

    public string SourceKind => Kind;

    public async Task<IReadOnlyList<PayInput>> GetInputsAsync(PayInputQuery query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var payouts = (await _ledger.ListPayoutsAsync(query.PeriodEnd, cancellationToken))
            .Where(entry => query.StaffMemberIds is not { Count: > 0 } || query.StaffMemberIds.Contains(entry.WorkerId))
            .Where(entry => query.IncludeConsumed
                ? query.PeriodStart is null || entry.EffectiveDate >= query.PeriodStart.Value
                : !IsConsumed(entry))
            .ToList();

        if (payouts.Count == 0)
        {
            return [];
        }

        var workers = (await _workers.GetAsync(payouts.Select(entry => entry.WorkerId).Distinct(StringComparer.Ordinal).ToArray(), cancellationToken))
            .ToDictionary(worker => worker.ItemId, StringComparer.Ordinal);
        var codes = await _codes.GetAllAsync(cancellationToken);
        var fallback = codes.FirstOrDefault(code => code.Enabled && code.Kind == PayCodeKinds.PaidTimeOff);
        var inputs = new List<PayInput>();

        foreach (var entry in payouts.OrderBy(entry => entry.EffectiveDate))
        {
            var policy = await _policies.FindByIdAsync(entry.PolicyId, cancellationToken);
            var type = policy is null ? null : await _types.FindByIdAsync(policy.TypeId, cancellationToken);
            var code = (type is null || string.IsNullOrEmpty(type.PayCode) ? null : codes.FirstOrDefault(candidate => string.Equals(candidate.Code, type.PayCode, StringComparison.OrdinalIgnoreCase))) ?? fallback;

            // An unpaid type pays nothing out; without a pay code there is nothing to pay it on.
            if (code is null || type?.IsPaid == false)
            {
                continue;
            }

            var worker = workers.GetValueOrDefault(entry.WorkerId);
            var rates = worker is not null && worker.TryGet<StaffPayRates>(out var stored) ? stored : new StaffPayRates();
            var amount = Math.Abs(entry.Amount);
            var hours = type?.Unit == TimeOffUnit.Days ? amount * (type.HoursPerDay > 0 ? type.HoursPerDay : 8) : amount;

            inputs.Add(new PayInput
            {
                SourceKind = Kind,
                SourceId = entry.ItemId,
                StaffMemberId = entry.WorkerId,
                StaffMemberName = worker?.DisplayName,
                EmployeeNumber = worker?.EmployeeNumber,
                PayrollId = worker?.ExternalId ?? worker?.EmployeeNumber,
                PayScheduleId = query.PayScheduleId,
                PeriodStart = query.PeriodStart ?? entry.EffectiveDate,
                PeriodEnd = query.PeriodEnd ?? entry.EffectiveDate,
                PayCodeId = code.ItemId,
                Date = entry.EffectiveDate,
                Hours = Math.Round(hours, 2),
                Rate = rates.RateOn(entry.EffectiveDate),
                Consumed = IsConsumed(entry),
            });
        }

        return inputs;
    }

    public async Task MarkConsumedAsync(string consumerId, IReadOnlyCollection<string> sourceIds, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(consumerId);
        ArgumentNullException.ThrowIfNull(sourceIds);

        foreach (var entry in await _ledger.GetAsync(sourceIds.ToArray(), cancellationToken))
        {
            var consumers = entry.TryGet<TimeOffPayoutConsumers>(out var stored) && stored is not null ? stored : new TimeOffPayoutConsumers();

            if (!consumers.ConsumerIds.Contains(consumerId))
            {
                consumers.ConsumerIds.Add(consumerId);
                entry.Put(consumers);
                await _ledger.UpdateAsync(entry, cancellationToken);
            }
        }
    }

    public async Task ReleaseAsync(string consumerId, IReadOnlyCollection<string> sourceIds, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(consumerId);
        ArgumentNullException.ThrowIfNull(sourceIds);

        foreach (var entry in await _ledger.GetAsync(sourceIds.ToArray(), cancellationToken))
        {
            if (entry.TryGet<TimeOffPayoutConsumers>(out var consumers) && consumers is not null && consumers.ConsumerIds.Remove(consumerId))
            {
                entry.Put(consumers);
                await _ledger.UpdateAsync(entry, cancellationToken);
            }
        }
    }

    private static bool IsConsumed(TimeOffLedgerEntry entry)
        => entry.TryGet<TimeOffPayoutConsumers>(out var consumers) && consumers?.ConsumerIds.Count > 0;
}

/// <summary>
/// Puts a staff member's time-off balances on their pay statement: each paid type they may see, as of the pay date.
/// </summary>
internal sealed class TimeOffPayStatementBalanceSource : IPayStatementBalanceSource
{
    private readonly ITimeOffBalanceService _balances;

    public TimeOffPayStatementBalanceSource(ITimeOffBalanceService balances)
    {
        _balances = balances;
    }

    public async Task<IReadOnlyList<PayStatementBalance>> GetBalancesAsync(string staffMemberId, DateOnly date, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(staffMemberId);

        return [.. (await _balances.GetBalancesAsync(staffMemberId, date, cancellationToken))
            .Where(balance => balance.Type is { VisibleToWorkers: true, Enabled: true })
            .OrderBy(balance => balance.Type.Name, StringComparer.CurrentCultureIgnoreCase)
            .Select(balance => new PayStatementBalance
            {
                Name = balance.Type.Name,
                Unit = balance.Type.Unit == TimeOffUnit.Days ? PayStatementBalance.Days : PayStatementBalance.Hours,
                Balance = Math.Round(balance.Current, 2),
            })];
    }
}
