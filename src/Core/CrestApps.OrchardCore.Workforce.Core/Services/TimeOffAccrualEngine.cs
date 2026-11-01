using System.Globalization;
using CrestApps.Core;
using CrestApps.OrchardCore.Workforce.Models;

namespace CrestApps.OrchardCore.Workforce.Core.Services;

/// <summary>
/// Works out the ledger entries a policy owes a worker: an accrual for each period (or reset date) that has passed,
/// and an expiry of what is above the carryover limit at each reset date. Each entry has a key made of the worker,
/// the policy and its day, so entries already in the ledger are never owed again.
/// </summary>
internal static class TimeOffAccrualEngine
{
    /// <summary>
    /// Returns the entries owed and not yet in the ledger, in date order.
    /// </summary>
    public static IReadOnlyList<TimeOffLedgerEntry> Compute(TimeOffAccrualInput input)
    {
        ArgumentNullException.ThrowIfNull(input);

        var policy = input.Policy;
        var worker = input.Worker;

        if (policy is null || worker is null || policy.AccrualMethod == TimeOffAccrualMethod.None)
        {
            return [];
        }

        var start = input.EligibleFrom;

        if (worker.HireDate.HasValue && worker.HireDate.Value.AddDays(Math.Max(0, policy.WaitingPeriodDays)) > start)
        {
            start = worker.HireDate.Value.AddDays(Math.Max(0, policy.WaitingPeriodDays));
        }

        var end = input.Date;

        if (input.EligibleTo.HasValue && input.EligibleTo.Value < end)
        {
            end = input.EligibleTo.Value;
        }

        if (worker.TerminationDate.HasValue && worker.TerminationDate.Value < end)
        {
            end = worker.TerminationDate.Value;
        }

        if (end < start)
        {
            return [];
        }

        var steps = new List<(DateOnly Date, bool Expiry, AccrualPeriod? Period)>();
        var resets = ResetDates(policy, start, end);

        if (policy.CarryoverMax.HasValue)
        {
            steps.AddRange(resets.Select(date => (date, true, (AccrualPeriod?)null)));
        }

        if (policy.AccrualMethod == TimeOffAccrualMethod.AnnualLumpSum)
        {
            if (!resets.Contains(start))
            {
                steps.Add((start, false, null));
            }

            steps.AddRange(resets.Select(date => (date, false, (AccrualPeriod?)null)));
        }
        else
        {
            steps.AddRange(Periods(policy, start, end).Select(period => (period.End, false, (AccrualPeriod?)period)));
        }

        var keys = new HashSet<string>(input.Existing.Select(entry => entry.IdempotencyKey).Where(key => key is not null), StringComparer.Ordinal);
        var ledger = new List<TimeOffLedgerEntry>(input.Existing);
        var result = new List<TimeOffLedgerEntry>();

        foreach (var step in steps.OrderBy(step => step.Date).ThenBy(step => step.Expiry ? 0 : 1))
        {
            if (input.AppliesOn is not null && !input.AppliesOn(step.Date))
            {
                continue;
            }

            var key = Key(step.Expiry ? "expiry" : "accrual", worker, policy, step.Date);

            if (keys.Contains(key))
            {
                continue;
            }

            TimeOffLedgerEntry entry;

            if (step.Expiry)
            {
                var excess = ledger.Where(item => item.EffectiveDate < step.Date).Sum(item => item.Amount) - policy.CarryoverMax.Value;

                if (excess <= 0)
                {
                    continue;
                }

                entry = NewEntry(worker, policy, TimeOffLedgerKind.Expiry, -excess, step.Date, key, step.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
            }
            else
            {
                var amount = Math.Round(AmountFor(input, start, step.Date, step.Period), 4);

                if (amount <= 0)
                {
                    continue;
                }

                string note = null;

                if (policy.MaxBalance.HasValue)
                {
                    var room = Math.Max(0, policy.MaxBalance.Value - ledger.Where(item => item.EffectiveDate <= step.Date).Sum(item => item.Amount));

                    if (amount > room)
                    {
                        amount = room;
                        note = string.Create(CultureInfo.InvariantCulture, $"Capped at the maximum balance of {policy.MaxBalance.Value:0.##}.");
                    }
                }

                var source = step.Period is { } period
                    ? string.Create(CultureInfo.InvariantCulture, $"{period.Start:yyyy-MM-dd}..{period.End:yyyy-MM-dd}")
                    : step.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

                entry = NewEntry(worker, policy, TimeOffLedgerKind.Accrual, amount, step.Date, key, source);
                entry.Note = note;
            }

            keys.Add(key);
            ledger.Add(entry);
            result.Add(entry);
        }

        return result;
    }

    /// <summary>
    /// Returns the key of an accrual or expiry entry.
    /// </summary>
    public static string Key(string kind, WorkerProfile worker, TimeOffPolicy policy, DateOnly date)
    {
        return string.Create(CultureInfo.InvariantCulture, $"{kind}:{worker.ItemId}:{policy.ItemId}:{date:yyyyMMdd}");
    }

    /// <summary>
    /// Returns the accrual periods whose last day falls within a range, in order.
    /// </summary>
    public static IReadOnlyList<AccrualPeriod> Periods(TimeOffPolicy policy, DateOnly from, DateOnly to)
    {
        ArgumentNullException.ThrowIfNull(policy);

        var periods = new List<AccrualPeriod>();

        if (to < from)
        {
            return periods;
        }

        switch (policy.Frequency)
        {
            case TimeOffAccrualFrequency.Weekly:
            case TimeOffAccrualFrequency.Biweekly:
                var length = policy.Frequency == TimeOffAccrualFrequency.Weekly ? 7 : 14;
                var anchor = policy.AnchorDate ?? from.AddDays(-(((int)from.DayOfWeek + 6) % 7));
                var first = (int)Math.Floor((from.DayNumber - anchor.DayNumber) / (double)length) - 1;

                for (var index = first; ; index++)
                {
                    var start = anchor.AddDays(index * length);
                    var end = start.AddDays(length - 1);

                    if (end > to)
                    {
                        break;
                    }

                    if (end >= from)
                    {
                        periods.Add(new AccrualPeriod(start, end));
                    }
                }

                break;

            case TimeOffAccrualFrequency.SemiMonthly:
                for (var month = new DateOnly(from.Year, from.Month, 1); month <= to; month = month.AddMonths(1))
                {
                    var middle = new DateOnly(month.Year, month.Month, 15);
                    var last = month.AddMonths(1).AddDays(-1);

                    Add(periods, new AccrualPeriod(month, middle), from, to);
                    Add(periods, new AccrualPeriod(middle.AddDays(1), last), from, to);
                }

                break;

            default:
                for (var month = new DateOnly(from.Year, from.Month, 1); month <= to; month = month.AddMonths(1))
                {
                    Add(periods, new AccrualPeriod(month, month.AddMonths(1).AddDays(-1)), from, to);
                }

                break;
        }

        return periods;
    }

    /// <summary>
    /// Returns a worker's whole months of service on a day.
    /// </summary>
    public static int MonthsOfService(WorkerProfile worker, DateOnly date)
    {
        var since = worker?.SeniorityDate ?? worker?.HireDate;

        if (!since.HasValue || date < since.Value)
        {
            return 0;
        }

        var months = ((date.Year - since.Value.Year) * 12) + date.Month - since.Value.Month;

        return date.Day < since.Value.Day ? months - 1 : months;
    }

    private static decimal AmountFor(TimeOffAccrualInput input, DateOnly start, DateOnly date, AccrualPeriod? period)
    {
        var policy = input.Policy;
        var rate = policy.RateFor(MonthsOfService(input.Worker, date));

        switch (policy.AccrualMethod)
        {
            case TimeOffAccrualMethod.PerHourWorked:
                return period is { } worked && input.WorkedMinutes.TryGetValue(worked.End, out var minutes)
                    ? rate * minutes / 60m
                    : 0;

            case TimeOffAccrualMethod.AnnualLumpSum:
                if (IsResetDate(policy, date) || date != start)
                {
                    return rate;
                }

                // The share of the year left when the worker becomes eligible.
                var next = ResetOnOrAfter(policy, date.AddDays(1));
                var previous = ResetOn(policy, next.Year - 1);

                return Math.Round(rate * (next.DayNumber - date.DayNumber) / (next.DayNumber - previous.DayNumber), 2);

            default:
                return rate;
        }
    }

    private static List<DateOnly> ResetDates(TimeOffPolicy policy, DateOnly from, DateOnly to)
    {
        var dates = new List<DateOnly>();

        for (var year = from.Year; year <= to.Year; year++)
        {
            var date = ResetOn(policy, year);

            if (date >= from && date <= to)
            {
                dates.Add(date);
            }
        }

        return dates;
    }

    private static bool IsResetDate(TimeOffPolicy policy, DateOnly date)
    {
        return ResetOn(policy, date.Year) == date;
    }

    private static DateOnly ResetOnOrAfter(TimeOffPolicy policy, DateOnly date)
    {
        var reset = ResetOn(policy, date.Year);

        return reset >= date ? reset : ResetOn(policy, date.Year + 1);
    }

    private static DateOnly ResetOn(TimeOffPolicy policy, int year)
    {
        var month = Math.Clamp(policy.ResetMonth, 1, 12);

        return new DateOnly(year, month, Math.Clamp(policy.ResetDay, 1, DateTime.DaysInMonth(year, month)));
    }

    private static void Add(List<AccrualPeriod> periods, AccrualPeriod period, DateOnly from, DateOnly to)
    {
        if (period.End >= from && period.End <= to)
        {
            periods.Add(period);
        }
    }

    private static TimeOffLedgerEntry NewEntry(WorkerProfile worker, TimeOffPolicy policy, TimeOffLedgerKind kind, decimal amount, DateOnly date, string key, string source)
    {
        return new TimeOffLedgerEntry
        {
            ItemId = UniqueId.GenerateId(),
            WorkerId = worker.ItemId,
            PolicyId = policy.ItemId,
            Kind = kind,
            Amount = amount,
            EffectiveDate = date,
            IdempotencyKey = key,
            SourceRef = source,
        };
    }
}
