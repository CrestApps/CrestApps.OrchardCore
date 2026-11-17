using System.Globalization;
using CrestApps.OrchardCore.Payroll.Models;
using CrestApps.OrchardCore.Payroll.Services;
using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.Payroll.Core.Calculation;

/// <summary>
/// Retro pay, in a regular pay: for each earlier statement of the year, an hours line of a code paid in retro pay at a
/// rate below the rate now on file for its period, and a full period's salary below the salary now on file, owe the
/// difference; what earlier retro lines already paid for that statement is taken off. Only raises are paid back: a rate
/// that went down is left to a person to settle. A profile's <c>RetroPayFrom</c> limits how far back it reaches.
/// </summary>
public sealed class RetroPayCalculationStep : IPayCalculationStep
{
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="RetroPayCalculationStep"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public RetroPayCalculationStep(IStringLocalizer<RetroPayCalculationStep> stringLocalizer)
    {
        S = stringLocalizer;
    }

    /// <inheritdoc />
    public int Order => PayCalculationOrder.RetroPay;

    /// <inheritdoc />
    public Task ExecuteAsync(PayCalculationContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (!context.IncludeSalary || context.History.Count == 0)
        {
            return Task.CompletedTask;
        }

        var paidBack = context.History
            .SelectMany(statement => statement.Lines)
            .Where(line => !string.IsNullOrEmpty(line.RetroFor))
            .GroupBy(line => (line.RetroFor, line.PayCodeId))
            .ToDictionary(group => group.Key, group => group.Sum(line => line.Amount));

        // Retro pay reaches back only to the profile's first day for it, when it has one.
        var reach = context.Profile?.RetroPayFrom;

        foreach (var statement in context.History.Where(statement => statement.VoidOf is null && statement.PeriodEnd < context.PeriodStart && (reach is null || statement.PeriodEnd >= reach)).OrderBy(statement => statement.PayDate))
        {
            var owed = new Dictionary<string, decimal>(StringComparer.Ordinal);

            foreach (var line in statement.Lines.Where(line => line.Type == PayLineType.Earning && string.IsNullOrEmpty(line.RetroFor)))
            {
                var code = context.Code(line.PayCodeId);

                if (code is null || !code.GetPayrollSettings().InRetroPay)
                {
                    continue;
                }

                decimal difference;

                if (code.Kind == PayCodeKinds.Salary)
                {
                    // The period priced again from the salary on file now, day by day as it was paid: a change in the
                    // period, a hire or a termination prorate the same way, so only a salary changed since is owed.
                    difference = context.Round(EarningsPayCalculationStep.SalaryFor(context, code, statement.PeriodStart, statement.PeriodEnd)) - line.Amount;
                }
                else if (line.RatedHours is { Count: > 0 } rated && line.Rate is > 0 && code.PremiumPerHour is not > 0)
                {
                    // Each day's hours against the rate on file now for that day; hours that came with their own rate
                    // (a training rate) were never priced from the rate on file and are left out.
                    difference = rated.Sum(day => context.Pay.RateOn(day.Date) is { } rate ? day.Hours * ((rate * code.Multiplier) - line.Rate.Value) : 0);
                }
                else
                {
                    continue;
                }

                owed[code.ItemId] = owed.GetValueOrDefault(code.ItemId) + difference;
            }

            foreach (var (codeId, difference) in owed)
            {
                var amount = context.Round(difference - paidBack.GetValueOrDefault((statement.ItemId, codeId)));

                if (amount <= 0)
                {
                    continue;
                }

                var code = context.Code(codeId);

                context.Lines.Add(new PayStatementLine
                {
                    Type = PayLineType.Earning,
                    PayCodeId = codeId,
                    Description = S["{0}: retro pay for {1} to {2}", code.Name, statement.PeriodStart.ToString("d", CultureInfo.CurrentCulture), statement.PeriodEnd.ToString("d", CultureInfo.CurrentCulture)],
                    Amount = amount,
                    RetroFor = statement.ItemId,
                    Allocation = context.Profile?.DefaultAllocation,
                });
            }
        }

        return Task.CompletedTask;
    }
}

/// <summary>
/// The tip credit: in each week of a tipped person, the cash wage of the hours worked and the tips that count (at most
/// the maximum tip credit an hour) must reach the minimum wage of those hours; what falls short is paid as a make-up.
/// Tips of a week are the tip lines dated in it; undated tips are spread over the weeks by hours.
/// </summary>
public sealed class TipCreditPayCalculationStep : IPayCalculationStep
{
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="TipCreditPayCalculationStep"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public TipCreditPayCalculationStep(IStringLocalizer<TipCreditPayCalculationStep> stringLocalizer)
    {
        S = stringLocalizer;
    }

    /// <inheritdoc />
    public int Order => PayCalculationOrder.TipCredit;

    /// <inheritdoc />
    public Task ExecuteAsync(PayCalculationContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context.Profile?.Tipped != true || context.MinimumHourlyWage is not { } minimum || context.Schedule is null)
        {
            return Task.CompletedTask;
        }

        var worked = context.Inputs
            .Select(input => (Input: input, Code: context.Code(input.PayCodeId)))
            .Where(pair => pair.Code is { Class: PayCodeClass.Earning } && pair.Input.Hours is > 0 && pair.Input.Amount is null && pair.Code.GetPayrollSettings().CountsAsHoursWorked)
            .Select(pair => (Week: context.Schedule.WeekOf(pair.Input.Date ?? context.PeriodEnd), Hours: pair.Input.Hours.Value, Cash: pair.Input.Hours.Value * (pair.Input.Rate ?? context.Pay.RateOn(pair.Input.Date ?? context.PeriodEnd) ?? 0)))
            .GroupBy(entry => entry.Week)
            .ToDictionary(group => group.Key, group => (Hours: group.Sum(entry => entry.Hours), Cash: group.Sum(entry => entry.Cash)));

        if (worked.Count == 0)
        {
            return Task.CompletedTask;
        }

        var tips = context.Inputs
            .Select(input => (Input: input, Code: context.Code(input.PayCodeId)))
            .Where(pair => pair.Code is { Kind: PayCodeKinds.Tips } && pair.Input.Amount is > 0)
            .ToList();
        var dated = tips.Where(pair => pair.Input.Date is { } date && worked.ContainsKey(context.Schedule.WeekOf(date)))
            .GroupBy(pair => context.Schedule.WeekOf(pair.Input.Date.Value))
            .ToDictionary(group => group.Key, group => group.Sum(pair => pair.Input.Amount.Value));
        var spread = tips.Where(pair => pair.Input.Date is not { } date || !worked.ContainsKey(context.Schedule.WeekOf(date))).Sum(pair => pair.Input.Amount.Value);
        var totalHours = worked.Values.Sum(week => week.Hours);
        var makeup = 0m;

        foreach (var (week, (hours, cash)) in worked)
        {
            var weekTips = dated.GetValueOrDefault(week) + (totalHours > 0 ? spread * hours / totalHours : 0);
            var credit = Math.Min(weekTips, (context.MaximumTipCredit ?? minimum) * hours);

            makeup += Math.Max(0, (minimum * hours) - cash - credit);
        }

        makeup = context.Round(makeup);

        if (makeup <= 0)
        {
            return Task.CompletedTask;
        }

        var code = context.Codes.Values.FirstOrDefault(candidate => candidate.Enabled && candidate.Class == PayCodeClass.Earning && candidate.Kind == PayCodeKinds.MinimumWageMakeup);

        if (code is null)
        {
            context.Warnings.Add(S["The cash wage and tips fell {0} short of the minimum wage, but no pay code pays a tip credit make-up: add one with Pays for set to it.", makeup.ToString("0.00", CultureInfo.InvariantCulture)]);

            return Task.CompletedTask;
        }

        context.Lines.Add(new PayStatementLine
        {
            Type = PayLineType.Earning,
            PayCodeId = code.ItemId,
            Description = code.Name,
            Amount = makeup,
            Allocation = context.Profile?.DefaultAllocation,
        });

        return Task.CompletedTask;
    }
}

/// <summary>
/// A bonus or commission earned in earlier weeks raises the regular rate of those weeks, so their overtime is owed more:
/// the amount divided by all the hours worked in the statements of those weeks, times half (the multiplier less one) of
/// each overtime hour, as the overtime premium of the overtime code with the most hours (29 CFR 778.209-210).
/// </summary>
public sealed class EarnedRegularRatePayCalculationStep : IPayCalculationStep
{
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="EarnedRegularRatePayCalculationStep"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public EarnedRegularRatePayCalculationStep(IStringLocalizer<EarnedRegularRatePayCalculationStep> stringLocalizer)
    {
        S = stringLocalizer;
    }

    /// <inheritdoc />
    public int Order => PayCalculationOrder.EarnedRegularRate;

    /// <inheritdoc />
    public Task ExecuteAsync(PayCalculationContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context.Profile?.OvertimeStatus == OvertimeStatus.Exempt)
        {
            return Task.CompletedTask;
        }

        foreach (var input in context.Inputs.Where(input => input.EarnedFrom is not null && input.EarnedTo is { } to && to < context.PeriodStart && input.Amount is > 0))
        {
            var code = context.Code(input.PayCodeId);

            if (code is not { Class: PayCodeClass.Earning } || !code.GetPayrollSettings().CountsInRegularRate)
            {
                continue;
            }

            // A statement whose period is only partly in the earning window counts the share of its hours in it: the
            // hours of its days in the window, when its lines kept them by day, or else its hours by the share of days.
            var from = input.EarnedFrom.Value;
            var to = input.EarnedTo.Value;
            var overlapping = context.History
                .Where(statement => statement.VoidOf is null && statement.PeriodEnd >= from && statement.PeriodStart <= to)
                .ToList();
            var partial = overlapping.Where(statement => statement.PeriodStart < from || statement.PeriodEnd > to).ToList();
            var lines = overlapping
                .SelectMany(statement => statement.Lines.Select(line => (Statement: statement, Line: line)))
                .Where(pair => pair.Line.Type == PayLineType.Earning && pair.Line.Hours is > 0 && string.IsNullOrEmpty(pair.Line.RetroFor))
                .Select(pair => (pair.Line, Code: context.Code(pair.Line.PayCodeId), Hours: HoursIn(pair.Statement, pair.Line, from, to)))
                .Where(pair => pair.Code is not null && pair.Code.GetPayrollSettings().CountsAsHoursWorked && pair.Hours > 0)
                .ToList();
            var hours = lines.Sum(pair => pair.Hours);
            var overtime = lines.Where(pair => pair.Code.Multiplier > 1)
                .GroupBy(pair => pair.Code.ItemId)
                .Select(group => (CodeId: group.Key, Weighted: group.Sum(pair => pair.Hours * (pair.Code.Multiplier - 1))))
                .ToList();

            if (partial.Count > 0)
            {
                context.Warnings.Add(S["{0} earned {1} to {2}: the pay periods {3} are partly in it, so only their share of hours in it counts.", code.Name, from.ToString("d", CultureInfo.CurrentCulture), to.ToString("d", CultureInfo.CurrentCulture), string.Join(", ", partial.Select(statement => statement.PeriodStart.ToString("d", CultureInfo.CurrentCulture) + "–" + statement.PeriodEnd.ToString("d", CultureInfo.CurrentCulture)))]);
            }
            var weighted = overtime.Sum(entry => entry.Weighted);

            if (hours <= 0 || weighted <= 0)
            {
                continue;
            }

            var premium = context.Round(input.Amount.Value / hours * weighted);

            if (premium <= 0)
            {
                continue;
            }

            var overtimeCode = context.Code(overtime.OrderByDescending(entry => entry.Weighted).First().CodeId);

            context.Lines.Add(new PayStatementLine
            {
                Type = PayLineType.Earning,
                PayCodeId = overtimeCode.ItemId,
                Description = S["{0}: premium on the regular rate raised by {1} earned {2} to {3}", overtimeCode.Name, code.Name, input.EarnedFrom.Value.ToString("d", CultureInfo.CurrentCulture), input.EarnedTo.Value.ToString("d", CultureInfo.CurrentCulture)],
                Amount = premium,
                Allocation = context.Profile?.DefaultAllocation,
            });
        }

        return Task.CompletedTask;
    }

    // The hours of a line in a window: by day when the line kept them, else by the share of its period's days.
    private static decimal HoursIn(PayStatement statement, PayStatementLine line, DateOnly from, DateOnly to)
    {
        if (statement.PeriodStart >= from && statement.PeriodEnd <= to)
        {
            return line.Hours ?? 0;
        }

        if (line.RatedHours is { Count: > 0 } days && days.Sum(day => day.Hours) == line.Hours)
        {
            return days.Where(day => day.Date >= from && day.Date <= to).Sum(day => day.Hours);
        }

        var first = statement.PeriodStart > from ? statement.PeriodStart : from;
        var last = statement.PeriodEnd < to ? statement.PeriodEnd : to;
        var periodDays = statement.PeriodEnd.DayNumber - statement.PeriodStart.DayNumber + 1;

        return last < first || periodDays <= 0 ? 0 : (line.Hours ?? 0) * (last.DayNumber - first.DayNumber + 1) / periodDays;
    }
}
