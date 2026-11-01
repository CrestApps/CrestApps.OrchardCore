using CrestApps.OrchardCore.Workforce.Models;
using CrestApps.OrchardCore.Workforce.Services;

namespace CrestApps.OrchardCore.Workforce.Core.Services;

/// <summary>
/// Gathers what the accrual engine needs for a worker and a policy: when the policy applies to them and, for accrual
/// per hour worked, the minutes worked in each period not yet paid.
/// </summary>
internal sealed class TimeOffAccrualPlanner
{
    private readonly IWorkedTimeSource _workedTime;

    public TimeOffAccrualPlanner(IWorkedTimeSource workedTime)
    {
        _workedTime = workedTime;
    }

    public async Task<TimeOffAccrualInput> BuildAsync(WorkerProfile worker, TimeOffPolicy policy, TimeOffPolicyResolver resolver, IReadOnlyList<TimeOffLedgerEntry> existing, DateOnly date, CancellationToken cancellationToken = default)
    {
        var input = new TimeOffAccrualInput
        {
            Policy = policy,
            Worker = worker,
            EligibleFrom = resolver.EligibleFrom(policy),
            EligibleTo = resolver.EligibleTo(policy),
            Date = date,
            Existing = existing,
            AppliesOn = day => resolver.AppliesOn(policy, day),
        };

        if (policy.AccrualMethod == TimeOffAccrualMethod.PerHourWorked)
        {
            var paid = new HashSet<string>(existing.Select(entry => entry.IdempotencyKey).Where(key => key is not null), StringComparer.Ordinal);
            var minutes = new Dictionary<DateOnly, int>();

            foreach (var period in TimeOffAccrualEngine.Periods(policy, input.EligibleFrom, date))
            {
                if (!paid.Contains(TimeOffAccrualEngine.Key("accrual", worker, policy, period.End)))
                {
                    minutes[period.End] = await _workedTime.GetWorkedMinutesAsync(worker, period.Start, period.End, cancellationToken);
                }
            }

            input.WorkedMinutes = minutes;
        }

        return input;
    }
}
