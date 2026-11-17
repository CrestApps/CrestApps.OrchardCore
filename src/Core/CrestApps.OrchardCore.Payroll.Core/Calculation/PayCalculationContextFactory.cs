using CrestApps.OrchardCore.Payroll.Core.Services;
using CrestApps.Core;
using CrestApps.OrchardCore.Payroll.Models;
using CrestApps.OrchardCore.Payroll.Services;
using CrestApps.OrchardCore.Staff.Models;
using CrestApps.OrchardCore.Staff.Services;
using CrestApps.OrchardCore.Teams.Models;
using CrestApps.OrchardCore.Teams.Services;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.Payroll.Core.Calculation;

/// <summary>
/// What a calculation of one pay is for: the period, the pay date and what the run includes.
/// </summary>
public sealed class PayCalculationRequest
{
    /// <summary>Gets or sets the first day of the period.</summary>
    public DateOnly PeriodStart { get; set; }

    /// <summary>Gets or sets the last day of the period.</summary>
    public DateOnly PeriodEnd { get; set; }

    /// <summary>Gets or sets the pay date.</summary>
    public DateOnly PayDate { get; set; }

    /// <summary>Gets or sets a value indicating whether the salary and the pay items apply (a regular run).</summary>
    public bool Regular { get; set; } = true;

    /// <summary>Gets or sets a value indicating whether the inputs of the pay input sources are read (a run of inputs only reads none).</summary>
    public bool IncludeSourceInputs { get; set; } = true;

    /// <summary>Gets or sets a value indicating whether inputs a run or an export already took are read again.</summary>
    public bool IncludeConsumedInputs { get; set; }

    /// <summary>Gets or sets inputs typed in for the run, added to the sources' inputs.</summary>
    public IList<PayInput> ExtraInputs { get; set; } = [];

    /// <summary>Gets or sets how supplemental pay is withheld.</summary>
    public PayrollSupplementalMethod SupplementalMethod { get; set; }
}

/// <summary>
/// Builds the calculation of a pay profile for a period from what the site holds: the employer, the schedule, the
/// staff member's pay and dates, the inputs of every source, the pay items, the pay codes, the accumulators, the home
/// and work jurisdictions and Payroll's settings.
/// </summary>
public sealed class PayCalculationContextFactory
{
    private readonly IEmployerManager _employers;
    private readonly IPayScheduleManager _schedules;
    private readonly IPayCodeManager _codes;
    private readonly IPayItemManager _items;
    private readonly IPayInputService _inputs;
    private readonly IPayAccumulatorService _accumulators;
    private readonly IStaffDirectory _staff;
    private readonly IServiceProvider _services;

    /// <summary>
    /// Initializes a new instance of the <see cref="PayCalculationContextFactory"/> class.
    /// </summary>
    /// <param name="employers">The employers.</param>
    /// <param name="schedules">The pay schedules.</param>
    /// <param name="codes">The pay codes.</param>
    /// <param name="items">The pay items.</param>
    /// <param name="inputs">The pay inputs.</param>
    /// <param name="accumulators">The accumulators.</param>
    /// <param name="staff">The staff directory.</param>
    /// <param name="services">The services, for the teams and the settings when they are there.</param>
    public PayCalculationContextFactory(
        IEmployerManager employers,
        IPayScheduleManager schedules,
        IPayCodeManager codes,
        IPayItemManager items,
        IPayInputService inputs,
        IPayAccumulatorService accumulators,
        IStaffDirectory staff,
        IServiceProvider services)
    {
        _employers = employers;
        _schedules = schedules;
        _codes = codes;
        _items = items;
        _inputs = inputs;
        _accumulators = accumulators;
        _staff = staff;
        _services = services;
    }

    /// <summary>
    /// Builds the calculation of a profile.
    /// </summary>
    /// <param name="profile">The pay profile.</param>
    /// <param name="request">The period, pay date and options.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The calculation, ready to run.</returns>
    public async Task<PayCalculationContext> CreateAsync(EmployeePayProfile profile, PayCalculationRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(request);

        var employer = await _employers.FindByIdAsync(profile.EmployerId, cancellationToken);
        var schedule = string.IsNullOrEmpty(profile.PayScheduleId) ? null : await _schedules.FindByIdAsync(profile.PayScheduleId, cancellationToken);
        var staff = (await _staff.GetAsync([profile.StaffMemberId], cancellationToken)).GetValueOrDefault(profile.StaffMemberId);
        var settings = _services.GetService<ISiteService>() is { } site ? await site.GetSettingsAsync<PayrollSettings>() : null;
        var inputs = !request.IncludeSourceInputs ? [] : (await _inputs.GetInputsAsync(new PayInputQuery
        {
            EmployerId = profile.EmployerId,
            PeriodStart = request.PeriodStart,
            PeriodEnd = request.PeriodEnd,
            StaffMemberIds = [profile.StaffMemberId],
            IncludeConsumed = request.IncludeConsumedInputs,
        }, cancellationToken)).Where(input => input.StaffMemberId == profile.StaffMemberId).ToList();

        inputs.AddRange(request.ExtraInputs);

        // Earlier final statements of the year (or of the earliest weeks an amount was earned in), for retro pay and
        // re-pricing: only when the pay is a regular one or an amount names earlier weeks.
        var earliest = inputs.Where(input => input.EarnedFrom is not null).Select(input => input.EarnedFrom.Value).DefaultIfEmpty(request.PayDate).Min();
        var from = new DateOnly(Math.Min(request.PayDate.Year, earliest.Year), 1, 1);
        var history = request.Regular || earliest < request.PayDate
            ? await HistoryAsync(profile, from, request.PeriodStart, cancellationToken)
            : [];

        return new PayCalculationContext
        {
            Profile = profile,
            Employer = employer,
            Schedule = schedule,
            PeriodStart = request.PeriodStart,
            PeriodEnd = request.PeriodEnd,
            PayDate = request.PayDate,
            Pay = staff is not null && staff.TryGet<StaffPayRates>(out var pay) && pay is not null ? pay : new StaffPayRates(),
            HireDate = staff?.HireDate,
            TerminationDate = staff?.TerminationDate,
            Inputs = inputs,
            Items = request.Regular ? [.. await _items.ListByProfileAsync(profile.ItemId, cancellationToken)] : [],
            IncludePayItems = request.Regular,
            IncludeSalary = request.Regular,
            Codes = (await _codes.GetAllAsync(cancellationToken)).ToDictionary(code => code.ItemId, StringComparer.Ordinal),
            History = history,
            SalaryPeriods = schedule?.ExtraPayPeriod == ExtraPayPeriodRule.DivideByPayDates ? PayDatesIn(schedule, request.PayDate.Year) : null,
            MinimumHourlyWage = settings?.MinimumHourlyWage,
            MaximumTipCredit = settings?.MaximumTipCredit,
            Garnishments = _services.GetService<IGarnishmentOrderManager>() is { } garnishments
                ? [.. (await garnishments.ListByProfileAsync(profile.ItemId, cancellationToken)).Where(order => order.AppliesOn(request.PayDate))]
                : [],
            Accumulators = await _accumulators.GetAsync(profile.StaffMemberId, profile.EmployerId, request.PayDate, request.PeriodStart, request.PeriodEnd, cancellationToken),
            SupplementalMethod = request.SupplementalMethod,
            MinimumNet = settings?.MinimumNetPay ?? 0,
            HomeJurisdictions = Home(profile),
            WorkJurisdictions = await WorkAsync(profile, cancellationToken),
        };
    }

    // The person's final statements with the employer whose period ended before this one, from a day on.
    private async Task<List<PayStatement>> HistoryAsync(EmployeePayProfile profile, DateOnly from, DateOnly before, CancellationToken cancellationToken)
    {
        if (_services.GetService<Services.PayStatementStore>() is not { } store)
        {
            return [];
        }

        var staffMemberId = profile.StaffMemberId;
        var employerId = profile.EmployerId;
        var fromDay = (from.Year * 10000) + (from.Month * 100) + from.Day;

        return [.. (await store.ListAsync(index => index.StaffMemberId == staffMemberId && index.EmployerId == employerId && index.PayDay >= fromDay, cancellationToken))
            .Where(statement => statement.Status is PayStatementStatus.Final or PayStatementStatus.Paid && statement.PeriodEnd < before)];
    }

    /// <summary>
    /// Counts the pay dates a schedule has in a calendar year (27 every two weeks in some years, 53 every week).
    /// </summary>
    /// <param name="schedule">The schedule.</param>
    /// <param name="year">The year.</param>
    /// <returns>The number of pay dates.</returns>
    public static int PayDatesIn(PaySchedule schedule, int year)
    {
        ArgumentNullException.ThrowIfNull(schedule);

        var count = 0;
        var day = new DateOnly(year, 1, 1).AddDays(-62);
        var last = new DateOnly(year, 12, 31).AddDays(62);

        while (day <= last)
        {
            var (_, end) = schedule.PeriodOn(day);

            if (schedule.PayDateFor(end).Year == year)
            {
                count++;
            }

            day = end.AddDays(1);
        }

        return count;
    }

    // The country and the country with its region of the home address, such as US and US-CA.
    private static List<string> Home(EmployeePayProfile profile)
    {
        var jurisdictions = new List<string>();

        if (!string.IsNullOrEmpty(profile.HomeAddress?.Country))
        {
            var country = profile.HomeAddress.Country.ToUpperInvariant();

            jurisdictions.Add(country);

            if (!string.IsNullOrEmpty(profile.HomeAddress.Region))
            {
                jurisdictions.Add($"{country}-{profile.HomeAddress.Region.ToUpperInvariant()}");
            }
        }

        return jurisdictions;
    }

    // The jurisdictions of the work site team.
    private Task<List<string>> WorkAsync(EmployeePayProfile profile, CancellationToken cancellationToken)
        => PayrollWorkSites.JurisdictionsAsync(_services, profile, cancellationToken);
}
