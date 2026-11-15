using CrestApps.OrchardCore.Payments;
using CrestApps.OrchardCore.Payroll.Models;
using CrestApps.OrchardCore.Payroll.Services;
using CrestApps.OrchardCore.Staff.Models;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.Payroll.Core.Calculation;

/// <summary>
/// What a pay input pays, the rule every reader of pay inputs shares (the export and the calculation).
/// </summary>
public static class PayInputAmount
{
    /// <summary>
    /// Works out what an input pays, not rounded: its amount; else nothing for unpaid time; else the premium for each
    /// hour of a code with a premium (the hours are paid by another line); else the hours times the rate times the
    /// code's multiplier.
    /// </summary>
    /// <param name="input">The input.</param>
    /// <param name="code">Its pay code.</param>
    /// <param name="rate">The rate when the input has none, or <see langword="null"/>.</param>
    /// <returns>The amount, or <see langword="null"/> when there is no rate.</returns>
    public static decimal? Of(PayInput input, PayCode code, decimal? rate = null)
    {
        ArgumentNullException.ThrowIfNull(input);

        if (input.Amount is { } amount)
        {
            return amount;
        }

        if (code is null)
        {
            return null;
        }

        if (code.Kind == PayCodeKinds.Unpaid)
        {
            return 0;
        }

        var hours = input.Hours ?? 0;

        if (code.PremiumPerHour is > 0)
        {
            return hours * code.PremiumPerHour.Value;
        }

        return (input.Rate ?? rate) is { } hourly ? hours * hourly * code.Multiplier : null;
    }
}

/// <summary>
/// What the steps share: the amount of a pay item or a deduction input, the limits, and adding a line.
/// </summary>
internal static class PayStepHelpers
{
    public static PayLineType LineTypeOf(PayCodeClass payCodeClass)
        => payCodeClass switch
        {
            PayCodeClass.Deduction => PayLineType.Deduction,
            PayCodeClass.EmployerContribution => PayLineType.EmployerContribution,
            PayCodeClass.ImputedIncome => PayLineType.ImputedIncome,
            PayCodeClass.Reimbursement => PayLineType.Reimbursement,
            PayCodeClass.Memo => PayLineType.Memo,
            _ => PayLineType.Earning,
        };

    // The earnings a percent is taken of: the base codes, or every earning.
    public static decimal BaseOf(PayCalculationContext context, PayCode code)
    {
        var baseIds = code.GetPayrollSettings().BaseCodeIds;

        return context.Lines
            .Where(line => line.Type == PayLineType.Earning && (baseIds.Count == 0 || baseIds.Contains(line.PayCodeId)))
            .Sum(line => line.Amount);
    }

    // What was already taken by a code: final statements, and lines of this calculation.
    public static PayLimitUsage UsageOf(PayCalculationContext context, PayCode code)
    {
        var totals = context.Accumulators.For(PayAccumulators.CodeKey(code.ItemId));
        var here = context.Lines.Where(line => line.PayCodeId == code.ItemId && line.Type != PayLineType.Tax).Sum(line => line.Amount);

        return new PayLimitUsage
        {
            ThisPeriod = totals.Period + here,
            YearToDate = totals.Year + here,
            LifetimeToDate = totals.Lifetime + here,
        };
    }

    // The pay items of a class that apply on the pay date, by priority.
    public static IEnumerable<(EmployeePayItem Item, PayCode Code)> ItemsOf(PayCalculationContext context, Func<PayCode, bool> filter)
        => context.IncludePayItems
            ? context.Items
                .Select(item => (Item: item, Code: context.Code(item.PayCodeId)))
                .Where(pair => pair.Code is { Enabled: true } && pair.Item.AppliesOn(context.PayDate) && filter(pair.Code))
                .OrderBy(pair => pair.Code.GetPayrollSettings().Priority)
            : [];

    // The inputs of a class, by priority.
    public static IEnumerable<(PayInput Input, PayCode Code)> InputsOf(PayCalculationContext context, Func<PayCode, bool> filter)
        => context.Inputs
            .Select(input => (Input: input, Code: context.Code(input.PayCodeId)))
            .Where(pair => pair.Code is not null && filter(pair.Code))
            .OrderBy(pair => pair.Code.GetPayrollSettings().Priority);

    // Takes a deduction or adds a contribution from a pay item or an input, within the limits.
    public static PayStatementLine AddLimited(PayCalculationContext context, PayCode code, EmployeePayItem item, PayInput input, decimal wanted, PayLinePayer payer)
    {
        if (item is not null && code.GetPayrollSettings().Arrears == PayCodeArrearsRule.CarryForward && item.ArrearsBalance > 0)
        {
            wanted += item.ArrearsBalance;
        }

        var limited = PayLimits.Apply(context.Round(wanted), code, item, UsageOf(context, code), context.PayDate);

        if (limited.Amount <= 0 && limited.Cut <= 0)
        {
            return null;
        }

        var line = new PayStatementLine
        {
            Type = LineTypeOf(code.Class),
            PayCodeId = code.ItemId,
            Description = code.Name,
            Amount = context.Round(limited.Amount),
            Cut = limited.Cut,
            Payer = payer,
            PayItemId = item?.ItemId,
            Allocation = input?.Allocation ?? context.Profile?.DefaultAllocation,
        };

        if (input is not null)
        {
            line.Sources.Add(new PayInputReference(input.SourceKind, input.SourceId));
        }

        context.Lines.Add(line);

        return line;
    }

    // What a pay item or a deduction input wants before its limits: an amount, or a percent of its base.
    public static decimal WantedOf(PayCalculationContext context, PayCode code, EmployeePayItem item, PayInput input, decimal? percentBase = null)
    {
        if (input is not null)
        {
            return input.Amount ?? 0;
        }

        if (item.Amount is { } amount)
        {
            return amount;
        }

        return item.Percent is { } percent ? (percentBase ?? BaseOf(context, code)) * percent / 100m : 0;
    }

    public static bool IsPreTax(PayCode code)
        => code.GetPayrollSettings().TaxTreatment.Values.Any(treatment => treatment == PayCodeTaxTreatment.Exempt);
}

/// <summary>
/// Earnings: the salary for the period (prorated by the code's rule for a hire, a termination or a salary change),
/// hours by rate and amounts from the inputs, recurring earnings and imputed income from pay items, and the earning of
/// a gross-up.
/// </summary>
public sealed class EarningsPayCalculationStep : IPayCalculationStep
{
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="EarningsPayCalculationStep"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public EarningsPayCalculationStep(IStringLocalizer<EarningsPayCalculationStep> stringLocalizer)
    {
        S = stringLocalizer;
    }

    /// <inheritdoc />
    public int Order => PayCalculationOrder.Earnings;

    /// <inheritdoc />
    public Task ExecuteAsync(PayCalculationContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        AddSalary(context);
        AddInputs(context);
        AddItems(context);

        if (context.GrossUp is { } grossUp && context.Code(grossUp.PayCodeId) is { } code)
        {
            context.Lines.Add(new PayStatementLine
            {
                Type = PayStepHelpers.LineTypeOf(code.Class),
                PayCodeId = code.ItemId,
                Description = code.Name,
                Amount = context.Round(grossUp.Amount),
                Allocation = context.Profile?.DefaultAllocation,
            });
        }

        return Task.CompletedTask;
    }

    private void AddSalary(PayCalculationContext context)
    {
        if (!context.IncludeSalary || context.Pay.PayBasis != StaffPayBasis.Salary)
        {
            return;
        }

        var code = context.Codes.Values.FirstOrDefault(candidate => candidate.Enabled && candidate.Class == PayCodeClass.Earning && candidate.Kind == PayCodeKinds.Salary);

        if (code is null)
        {
            context.Warnings.Add(S["No pay code pays a salary: add one with Pays for set to Salary."]);

            return;
        }

        var periods = context.SalaryPeriods ?? PayCalculator.PeriodsPerYear(context.Schedule?.Frequency ?? PayFrequency.Biweekly);
        var proration = code.GetPayrollSettings().Proration;
        var first = context.HireDate is { } hired && hired > context.PeriodStart ? hired : context.PeriodStart;
        var last = context.TerminationDate is { } left && left < context.PeriodEnd ? left : context.PeriodEnd;

        if (last < first)
        {
            return;
        }

        decimal amount;

        if (proration == PayCodeProrationRule.None)
        {
            amount = (context.Pay.SalaryOn(last) ?? 0) / periods;
        }
        else
        {
            // Each counted day of the period pays its share of the salary in effect that day, so a hire, a termination
            // and a salary change in the period are all prorated the same way.
            var days = CountedDays(context.PeriodStart, context.PeriodEnd, proration).ToList();

            if (days.Count == 0)
            {
                return;
            }

            amount = days
                .Where(day => day >= first && day <= last)
                .Sum(day => (context.Pay.SalaryOn(day) ?? 0) / periods / days.Count);
        }

        if (amount <= 0)
        {
            return;
        }

        context.Lines.Add(new PayStatementLine
        {
            Type = PayLineType.Earning,
            PayCodeId = code.ItemId,
            Description = code.Name,
            Amount = context.Round(amount),
            Allocation = context.Profile?.DefaultAllocation,
        });
    }

    private void AddInputs(PayCalculationContext context)
    {
        var lines = new Dictionary<(string Code, decimal? Rate, string Department, string Site, string Activity, string Job), PayStatementLine>();

        foreach (var input in context.Inputs)
        {
            var code = context.Code(input.PayCodeId);

            if (code is null)
            {
                context.Warnings.Add(S["An input of {0} names a pay code that no longer exists.", input.SourceKind ?? string.Empty]);

                continue;
            }

            if (code.Class is not (PayCodeClass.Earning or PayCodeClass.ImputedIncome or PayCodeClass.Memo))
            {
                continue;
            }

            var rate = input.Rate ?? (input.Hours is not null ? context.Pay.RateOn(input.Date ?? context.PeriodEnd) : null);
            var amount = PayInputAmount.Of(input, code, rate);

            if (amount is null)
            {
                context.Warnings.Add(S["{0} hours of {1} have no pay rate: enter one on the staff member's Pay card.", input.Hours ?? 0, code.Code]);

                continue;
            }

            var allocation = input.Allocation ?? context.Profile?.DefaultAllocation ?? new PayInputAllocation();
            (string Code, decimal? Rate, string Department, string Site, string Activity, string Job) key = (code.ItemId, input.Amount is null && code.PremiumPerHour is not > 0 ? rate : null, allocation.DepartmentTeamId, allocation.SiteTeamId, allocation.ActivityCode, allocation.Job);

            if (!lines.TryGetValue(key, out var line))
            {
                line = new PayStatementLine
                {
                    Type = PayStepHelpers.LineTypeOf(code.Class),
                    PayCodeId = code.ItemId,
                    Description = code.Name,
                    Rate = key.Rate is { } lineRate ? lineRate * code.Multiplier : code.PremiumPerHour,
                    Allocation = allocation,
                };
                lines[key] = line;
                context.Lines.Add(line);
            }

            line.Hours = input.Hours is { } hours ? (line.Hours ?? 0) + hours : line.Hours;
            line.Amount += amount.Value;
            line.Sources.Add(new PayInputReference(input.SourceKind, input.SourceId));
        }

        foreach (var line in lines.Values)
        {
            line.Amount = context.Round(line.Amount);
        }
    }

    private static void AddItems(PayCalculationContext context)
    {
        foreach (var (item, code) in PayStepHelpers.ItemsOf(context, code => code.Class is PayCodeClass.Earning or PayCodeClass.ImputedIncome))
        {
            PayStepHelpers.AddLimited(context, code, item, null, PayStepHelpers.WantedOf(context, code, item, null), PayLinePayer.Employer);
        }
    }

    /// <summary>
    /// Gets the days of a period a proration rule counts: every day, or Monday to Friday.
    /// </summary>
    /// <param name="start">The first day.</param>
    /// <param name="end">The last day.</param>
    /// <param name="rule">The rule.</param>
    /// <returns>The days.</returns>
    public static IEnumerable<DateOnly> CountedDays(DateOnly start, DateOnly end, PayCodeProrationRule rule)
    {
        for (var day = start; day <= end; day = day.AddDays(1))
        {
            if (rule != PayCodeProrationRule.WorkingDays || day.DayOfWeek is not (DayOfWeek.Saturday or DayOfWeek.Sunday))
            {
                yield return day;
            }
        }
    }
}

/// <summary>
/// The regular rate: in each overtime week, the pay for hours worked at their straight rate and the other pay that
/// counts (premiums, bonuses and commissions of the week, or spread over the period's weeks by hours) divided by the
/// hours worked. When it is above the rate an overtime line was paid at, the overtime premium it requires is added.
/// Exempt profiles have none.
/// </summary>
public sealed class RegularRatePayCalculationStep : IPayCalculationStep
{
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="RegularRatePayCalculationStep"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public RegularRatePayCalculationStep(IStringLocalizer<RegularRatePayCalculationStep> stringLocalizer)
    {
        S = stringLocalizer;
    }

    /// <inheritdoc />
    public int Order => PayCalculationOrder.RegularRate;

    /// <inheritdoc />
    public Task ExecuteAsync(PayCalculationContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context.Profile?.OvertimeStatus == OvertimeStatus.Exempt || context.Schedule is null)
        {
            return Task.CompletedTask;
        }

        var worked = context.Inputs
            .Select(input => (Input: input, Code: context.Code(input.PayCodeId)))
            .Where(pair => pair.Code is { Class: PayCodeClass.Earning } && pair.Input.Hours is > 0 && pair.Input.Amount is null && pair.Code.GetPayrollSettings().CountsAsHoursWorked)
            .Select(pair => (pair.Input, pair.Code, Week: context.Schedule.WeekOf(pair.Input.Date ?? context.PeriodEnd), Rate: pair.Input.Rate ?? context.Pay.RateOn(pair.Input.Date ?? context.PeriodEnd) ?? 0))
            .ToList();

        if (!worked.Any(entry => entry.Code.Multiplier > 1))
        {
            return Task.CompletedTask;
        }

        var weeks = worked.GroupBy(entry => entry.Week).ToDictionary(group => group.Key, group => group.ToList());
        var totalHours = worked.Sum(entry => entry.Input.Hours.Value);

        // Other pay that counts: dated inputs go to their week; undated ones and pay items are spread by hours.
        var dated = new Dictionary<DateOnly, decimal>();
        var spread = 0m;

        foreach (var input in context.Inputs)
        {
            var code = context.Code(input.PayCodeId);

            if (code is not { Class: PayCodeClass.Earning } || !code.GetPayrollSettings().CountsInRegularRate || (code.GetPayrollSettings().CountsAsHoursWorked && input.Amount is null))
            {
                continue;
            }

            var amount = PayInputAmount.Of(input, code) ?? 0;

            if (input.Date is { } date && weeks.ContainsKey(context.Schedule.WeekOf(date)))
            {
                dated[context.Schedule.WeekOf(date)] = dated.GetValueOrDefault(context.Schedule.WeekOf(date)) + amount;
            }
            else
            {
                spread += amount;
            }
        }

        spread += context.Lines
            .Where(line => line.PayItemId is not null && line.Type == PayLineType.Earning && context.Code(line.PayCodeId)?.GetPayrollSettings().CountsInRegularRate == true)
            .Sum(line => line.Amount);

        var premiums = new Dictionary<string, decimal>();

        foreach (var (week, entries) in weeks)
        {
            var hours = entries.Sum(entry => entry.Input.Hours.Value);

            if (hours <= 0)
            {
                continue;
            }

            var straight = entries.Sum(entry => entry.Input.Hours.Value * entry.Rate)
                + dated.GetValueOrDefault(week)
                + (totalHours > 0 ? spread * hours / totalHours : 0);
            var regularRate = straight / hours;

            foreach (var entry in entries.Where(entry => entry.Code.Multiplier > 1 && regularRate > entry.Rate))
            {
                premiums[entry.Code.ItemId] = premiums.GetValueOrDefault(entry.Code.ItemId) + ((entry.Code.Multiplier - 1) * (regularRate - entry.Rate) * entry.Input.Hours.Value);
            }
        }

        foreach (var (codeId, premium) in premiums)
        {
            var amount = context.Round(premium);

            if (amount <= 0)
            {
                continue;
            }

            context.Lines.Add(new PayStatementLine
            {
                Type = PayLineType.Earning,
                PayCodeId = codeId,
                Description = S["{0}: premium on the regular rate", context.Code(codeId).Name],
                Amount = amount,
                Allocation = context.Profile?.DefaultAllocation,
            });
        }

        return Task.CompletedTask;
    }
}

/// <summary>
/// Deductions taken before at least one tax (a code not taxed for a category), by priority, within their limits.
/// </summary>
public sealed class PreTaxDeductionsPayCalculationStep : IPayCalculationStep
{
    /// <inheritdoc />
    public int Order => PayCalculationOrder.PreTaxDeductions;

    /// <inheritdoc />
    public Task ExecuteAsync(PayCalculationContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        DeductionSteps.Take(context, code => PayStepHelpers.IsPreTax(code) && code.GetPayrollSettings().Calculation != PayCodeCalculation.PercentOfDisposableEarnings);

        return Task.CompletedTask;
    }
}

/// <summary>
/// Taxes: the regular and supplemental wages of each tax category from the treatment matrix, worked out by the tax
/// provider the settings name (or the only one there is). A contractor has nothing withheld.
/// </summary>
public sealed class TaxesPayCalculationStep : IPayCalculationStep
{
    private readonly IEnumerable<IPayrollTaxProvider> _providers;
    private readonly PayrollTaxCategoryOptions _categories;
    private readonly IServiceProvider _services;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="TaxesPayCalculationStep"/> class.
    /// </summary>
    /// <param name="providers">The tax providers.</param>
    /// <param name="categories">The tax categories.</param>
    /// <param name="services">The services, for the settings when there are any.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public TaxesPayCalculationStep(IEnumerable<IPayrollTaxProvider> providers, IOptions<PayrollTaxCategoryOptions> categories, IServiceProvider services, IStringLocalizer<TaxesPayCalculationStep> stringLocalizer)
    {
        _providers = providers;
        _categories = categories.Value;
        _services = services;
        S = stringLocalizer;
    }

    /// <inheritdoc />
    public int Order => PayCalculationOrder.Taxes;

    /// <inheritdoc />
    public async Task ExecuteAsync(PayCalculationContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context.Profile?.WorkerType == PayWorkerType.Contractor)
        {
            return;
        }

        var provider = await ProviderAsync();

        if (provider is null)
        {
            context.Warnings.Add(S["No tax provider is set up: no taxes were withheld."]);

            return;
        }

        var request = Request(context);
        var result = await provider.CalculateAsync(request, cancellationToken);

        foreach (var tax in result.Lines.Where(tax => tax.Amount != 0))
        {
            context.Lines.Add(new PayStatementLine
            {
                Type = PayLineType.Tax,
                TaxCode = tax.TaxCode,
                TaxCategory = tax.Category,
                Description = tax.Name,
                Jurisdiction = tax.Jurisdiction,
                Payer = tax.Payer,
                TaxableWages = context.Round(tax.TaxableWages),
                Amount = context.Round(tax.Amount),
            });
        }

        context.Warnings.AddRange(result.Warnings);
    }

    /// <summary>
    /// Builds the tax request of a calculation: wages per category, split into regular and supplemental.
    /// </summary>
    /// <param name="context">The calculation.</param>
    /// <returns>The request.</returns>
    public PayrollTaxRequest Request(PayCalculationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var amounts = context.Lines
            .Where(line => line.Type != PayLineType.Tax && context.Code(line.PayCodeId) is not null)
            .Select(line => (Line: line, Code: context.Code(line.PayCodeId)))
            .ToList();
        var regular = amounts.Where(pair => !pair.Code.GetPayrollSettings().Supplemental).Select(pair => new PayCodeAmount(pair.Code, pair.Line.Amount)).ToList();
        var supplemental = amounts.Where(pair => pair.Code.GetPayrollSettings().Supplemental && pair.Code.Class != PayCodeClass.Deduction).Select(pair => new PayCodeAmount(pair.Code, pair.Line.Amount)).ToList();
        var categories = _categories.Categories.Select(category => category.Name)
            .Concat(amounts.SelectMany(pair => pair.Code.GetPayrollSettings().TaxTreatment.Keys))
            .Distinct(StringComparer.Ordinal);
        var request = new PayrollTaxRequest
        {
            Employer = context.Employer,
            StaffMemberId = context.Profile?.StaffMemberId,
            WorkerType = context.Profile?.WorkerType ?? PayWorkerType.Employee,
            HomeJurisdictions = context.HomeJurisdictions,
            WorkJurisdictions = context.WorkJurisdictions,
            PayDate = context.PayDate,
            Frequency = context.Schedule?.Frequency ?? PayFrequency.Biweekly,
            PeriodsPerYear = PayCalculator.PeriodsPerYear(context.Schedule?.Frequency ?? PayFrequency.Biweekly),
            SupplementalMethod = context.SupplementalMethod,
            Accumulators = context.Accumulators,
            Currency = context.Currency,
            Elections = [.. (context.Profile?.Elections ?? [])
                .Select(election => election.Jurisdiction)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Select(jurisdiction => context.Profile.ElectionOn(jurisdiction, context.PayDate))
                .Where(election => election is not null)],
        };

        // What was paid earlier in the year, from the pay codes' totals, for taxes that start above a threshold.
        var codePrefix = PayAccumulators.CodeKey(string.Empty);
        var year = context.Accumulators.Totals
            .Where(pair => pair.Key.StartsWith(codePrefix, StringComparison.Ordinal))
            .Select(pair => (Code: context.Code(pair.Key[codePrefix.Length..]), pair.Value.Year))
            .Where(pair => pair.Code is not null && pair.Year != 0)
            .ToList();
        var yearRegular = year.Where(pair => !pair.Code.GetPayrollSettings().Supplemental).Select(pair => new PayCodeAmount(pair.Code, pair.Year)).ToList();
        var yearSupplemental = year.Where(pair => pair.Code.GetPayrollSettings().Supplemental && pair.Code.Class != PayCodeClass.Deduction).Select(pair => new PayCodeAmount(pair.Code, pair.Year)).ToList();

        foreach (var category in categories)
        {
            request.Wages[category] = PayCodeTaxMatrix.WagesFor(category, regular);
            request.SupplementalWages[category] = PayCodeTaxMatrix.WagesFor(category, supplemental);
            request.YearSupplementalWages[category] = PayCodeTaxMatrix.WagesFor(category, yearSupplemental);
            request.YearWages[category] = PayCodeTaxMatrix.WagesFor(category, yearRegular) + request.YearSupplementalWages[category];
        }

        return request;
    }

    private async Task<IPayrollTaxProvider> ProviderAsync()
    {
        var name = _services.GetService(typeof(ISiteService)) is ISiteService site
            ? (await site.GetSettingsAsync<PayrollSettings>())?.TaxProvider
            : null;

        return string.IsNullOrEmpty(name)
            ? (_providers.Count() == 1 ? _providers.First() : null)
            : _providers.FirstOrDefault(provider => provider.Name == name);
    }
}

/// <summary>
/// Deductions taken after every tax, by priority, within their limits.
/// </summary>
public sealed class PostTaxDeductionsPayCalculationStep : IPayCalculationStep
{
    /// <inheritdoc />
    public int Order => PayCalculationOrder.PostTaxDeductions;

    /// <inheritdoc />
    public Task ExecuteAsync(PayCalculationContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        DeductionSteps.Take(context, code => !PayStepHelpers.IsPreTax(code) && code.GetPayrollSettings().Calculation != PayCodeCalculation.PercentOfDisposableEarnings);

        return Task.CompletedTask;
    }
}

/// <summary>
/// Deductions that are a percent of disposable earnings (gross less the employee's taxes), such as a garnishment.
/// </summary>
public sealed class DisposableEarningsPayCalculationStep : IPayCalculationStep
{
    /// <inheritdoc />
    public int Order => PayCalculationOrder.Garnishments;

    /// <inheritdoc />
    public Task ExecuteAsync(PayCalculationContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        var disposable = context.Gross - context.Lines.Where(line => line.Type == PayLineType.Tax && line.Payer == PayLinePayer.Employee).Sum(line => line.Amount);

        DeductionSteps.Take(context, code => code.GetPayrollSettings().Calculation == PayCodeCalculation.PercentOfDisposableEarnings, Math.Max(0, disposable));

        return Task.CompletedTask;
    }
}

/// <summary>
/// The deductions of a step, from pay items and inputs.
/// </summary>
internal static class DeductionSteps
{
    public static void Take(PayCalculationContext context, Func<PayCode, bool> filter, decimal? percentBase = null)
    {
        bool Deduction(PayCode code)
            => code.Class == PayCodeClass.Deduction && filter(code);

        var entries = PayStepHelpers.ItemsOf(context, Deduction).Select(pair => (pair.Item, Input: (PayInput)null, pair.Code))
            .Concat(PayStepHelpers.InputsOf(context, Deduction).Select(pair => (Item: (EmployeePayItem)null, pair.Input, pair.Code)))
            .OrderBy(entry => entry.Code.GetPayrollSettings().Priority);

        foreach (var (item, input, code) in entries)
        {
            PayStepHelpers.AddLimited(context, code, item, input, PayStepHelpers.WantedOf(context, code, item, input, percentBase), PayLinePayer.Employee);
        }
    }
}

/// <summary>
/// The net check: when net pay is below the minimum, deductions taken after tax are cut, the largest priority number
/// first; what a deduction that carries arrears could not take is owed for the next pay. Deductions taken before tax
/// are never cut here, since their taxes were already worked out.
/// </summary>
public sealed class NetCheckPayCalculationStep : IPayCalculationStep
{
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="NetCheckPayCalculationStep"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public NetCheckPayCalculationStep(IStringLocalizer<NetCheckPayCalculationStep> stringLocalizer)
    {
        S = stringLocalizer;
    }

    /// <inheritdoc />
    public int Order => PayCalculationOrder.NetCheck;

    /// <inheritdoc />
    public Task ExecuteAsync(PayCalculationContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        var deductions = context.Lines
            .Where(line => line.Type == PayLineType.Deduction && context.Code(line.PayCodeId) is { } code && !PayStepHelpers.IsPreTax(code))
            .OrderByDescending(line => context.Code(line.PayCodeId).GetPayrollSettings().Priority)
            .ToList();

        foreach (var line in deductions)
        {
            var shortfall = context.MinimumNet - context.Net;

            if (shortfall <= 0)
            {
                break;
            }

            var cut = Math.Min(line.Amount, shortfall);
            var code = context.Code(line.PayCodeId);

            line.Amount -= cut;
            line.Cut += cut;

            if (code.GetPayrollSettings().Arrears == PayCodeArrearsRule.CarryForward)
            {
                line.ArrearsAdded += cut;
                context.Warnings.Add(S["{0} was cut by {1} because net pay was too small; it is owed for the next pay.", code.Name, cut]);
            }
            else
            {
                context.Warnings.Add(S["{0} was cut by {1} because net pay was too small.", code.Name, cut]);
            }
        }

        if (context.Net < context.MinimumNet)
        {
            context.Warnings.Add(S["Net pay is below the minimum even with no deductions taken after tax."]);
        }

        return Task.CompletedTask;
    }
}

/// <summary>
/// Employer contributions (from pay items and inputs, and the employer's match of a deduction item) and reimbursements.
/// </summary>
public sealed class ContributionsPayCalculationStep : IPayCalculationStep
{
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="ContributionsPayCalculationStep"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public ContributionsPayCalculationStep(IStringLocalizer<ContributionsPayCalculationStep> stringLocalizer)
    {
        S = stringLocalizer;
    }

    /// <inheritdoc />
    public int Order => PayCalculationOrder.Contributions;

    /// <inheritdoc />
    public Task ExecuteAsync(PayCalculationContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        bool Contribution(PayCode code)
            => code.Class == PayCodeClass.EmployerContribution;

        foreach (var (item, code) in PayStepHelpers.ItemsOf(context, Contribution))
        {
            PayStepHelpers.AddLimited(context, code, item, null, PayStepHelpers.WantedOf(context, code, item, null), PayLinePayer.Employer);
        }

        foreach (var (input, code) in PayStepHelpers.InputsOf(context, Contribution))
        {
            PayStepHelpers.AddLimited(context, code, null, input, input.Amount ?? 0, PayLinePayer.Employer);
        }

        // The employer's match of a deduction: a percent of what was taken, at most a percent of the earnings.
        foreach (var line in context.Lines.Where(line => line.Type == PayLineType.Deduction && line.PayItemId is not null).ToList())
        {
            var item = context.Items.FirstOrDefault(candidate => candidate.ItemId == line.PayItemId);

            if (item?.EmployerMatchPercent is not { } match || match <= 0 || context.Code(line.PayCodeId) is not { } code)
            {
                continue;
            }

            var amount = line.Amount * match / 100m;

            if (item.EmployerMatchLimitPercent is { } limit)
            {
                amount = Math.Min(amount, PayStepHelpers.BaseOf(context, code) * limit / 100m);
            }

            amount = context.Round(amount);

            if (amount > 0)
            {
                context.Lines.Add(new PayStatementLine
                {
                    Type = PayLineType.EmployerContribution,
                    PayCodeId = code.ItemId,
                    Description = S["{0}: employer match", code.Name],
                    Amount = amount,
                    Payer = PayLinePayer.Employer,
                    PayItemId = item.ItemId,
                    Allocation = line.Allocation,
                });
            }
        }

        bool Reimbursement(PayCode code)
            => code.Class == PayCodeClass.Reimbursement;

        foreach (var (item, code) in PayStepHelpers.ItemsOf(context, Reimbursement))
        {
            PayStepHelpers.AddLimited(context, code, item, null, PayStepHelpers.WantedOf(context, code, item, null), PayLinePayer.Employer);
        }

        foreach (var (input, code) in PayStepHelpers.InputsOf(context, Reimbursement))
        {
            PayStepHelpers.AddLimited(context, code, null, input, input.Amount ?? 0, PayLinePayer.Employer);
        }

        return Task.CompletedTask;
    }
}

/// <summary>
/// The split of net pay: the profile's rows in order, an amount, a percent of net pay or what is left; with no row,
/// a check.
/// </summary>
public sealed class DistributionPayCalculationStep : IPayCalculationStep
{
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="DistributionPayCalculationStep"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public DistributionPayCalculationStep(IStringLocalizer<DistributionPayCalculationStep> stringLocalizer)
    {
        S = stringLocalizer;
    }

    /// <inheritdoc />
    public int Order => PayCalculationOrder.Distribution;

    /// <inheritdoc />
    public Task ExecuteAsync(PayCalculationContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        var net = context.Round(context.Net);

        if (net <= 0)
        {
            return Task.CompletedTask;
        }

        var rows = context.Profile?.Distribution ?? [];

        if (rows.Count == 0)
        {
            context.Warnings.Add(S["The profile says nothing about how net pay is paid: it is paid by check."]);
            context.Payments.Add(new PayStatementPayment { Method = NetPayMethod.Check, Amount = net });

            return Task.CompletedTask;
        }

        // Percent rows share net pay by their percents, rounded so their shares add up exactly.
        var percents = rows.Select(row => row.Split == NetPaySplit.Percent ? row.Percent ?? 0 : 0).ToList();
        var percentTotal = percents.Sum();
        var percentShares = percentTotal > 0
            ? Money.Allocate(context.Round(net * Math.Min(percentTotal, 100) / 100m), percents, context.Currency)
            : new decimal[rows.Count];
        var left = net;

        for (var i = 0; i < rows.Count; i++)
        {
            var row = rows[i];
            var amount = row.Split switch
            {
                NetPaySplit.Amount => Math.Min(left, row.Amount ?? 0),
                NetPaySplit.Percent => Math.Min(left, percentShares[i]),
                _ => left,
            };

            if (amount <= 0)
            {
                continue;
            }

            context.Payments.Add(new PayStatementPayment { Method = row.Method, BankAccountId = row.BankAccountId, Amount = amount });
            left -= amount;
        }

        if (left > 0)
        {
            context.Payments.Add(new PayStatementPayment { Method = NetPayMethod.Check, Amount = left });
            context.Warnings.Add(S["The split of net pay left {0} unpaid: it is paid by check.", left]);
        }

        return Task.CompletedTask;
    }
}
